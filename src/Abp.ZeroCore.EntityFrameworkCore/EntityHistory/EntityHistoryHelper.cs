using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Transactions;
using Abp.Auditing;
using Abp.Dependency;
using Abp.Domain.Entities;
using Abp.Domain.Uow;
using Abp.EntityHistory.Extensions;
using Abp.Events.Bus.Entities;
using Abp.Extensions;
using Abp.Json;
using JetBrains.Annotations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Abp.EntityHistory
{
    public class EntityHistoryHelper : EntityHistoryHelperBase, IEntityHistoryHelper, ITransientDependency
    {
        public EntityHistoryHelper(
            IEntityHistoryConfiguration configuration,
            IUnitOfWorkManager unitOfWorkManager)
            : base(configuration, unitOfWorkManager)
        {
        }

        public virtual EntityChangeSet CreateEntityChangeSet(ICollection<EntityEntry> entityEntries)
        {
            var changeSet = new EntityChangeSet
            {
                Reason = EntityChangeSetReasonProvider.Reason.TruncateWithPostfix(EntityChangeSet.MaxReasonLength),

                // Fill "who did this change"
                BrowserInfo = ClientInfoProvider.BrowserInfo.TruncateWithPostfix(EntityChangeSet.MaxBrowserInfoLength),
                ClientIpAddress = ClientInfoProvider.ClientIpAddress.TruncateWithPostfix(EntityChangeSet.MaxClientIpAddressLength),
                ClientName = ClientInfoProvider.ComputerName.TruncateWithPostfix(EntityChangeSet.MaxClientNameLength),
                ImpersonatorTenantId = AbpSession.ImpersonatorTenantId,
                ImpersonatorUserId = AbpSession.ImpersonatorUserId,
                TenantId = AbpSession.TenantId,
                UserId = AbpSession.UserId
            };

            if (!IsEntityHistoryEnabled)
            {
                return changeSet;
            }

            foreach (var entityEntry in entityEntries)
            {
                var typeOfEntity = entityEntry.Entity.GetType();
                var shouldTrackEntity = IsTypeOfTrackedEntity(typeOfEntity);
                if (shouldTrackEntity.HasValue && !shouldTrackEntity.Value)
                {
                    continue;
                }

                if (!IsTypeOfEntity(typeOfEntity) && !entityEntry.Metadata.IsOwned())
                {
                    continue;
                }

                var shouldAuditEntity = IsTypeOfAuditedEntity(typeOfEntity);
                bool? shouldAuditOwnerProperty = null;
                bool? shouldAuditOwnerEntity = null;
                if (shouldAuditEntity.HasValue && !shouldAuditEntity.Value)
                {
                    continue;
                }
                else if (!shouldAuditEntity.HasValue && entityEntry.Metadata.IsOwned())
                {
                    // Check if owner entity has auditing attribute
                    var foreignKey = entityEntry.Metadata.GetForeignKeys().First();
                    var ownerEntity = foreignKey.PrincipalEntityType.ClrType;

                    shouldAuditOwnerEntity = IsTypeOfAuditedEntity(ownerEntity);
                    if (shouldAuditOwnerEntity.HasValue && !shouldAuditOwnerEntity.Value)
                    {
                        continue;
                    }

                    var ownerPropertyInfo = foreignKey.PrincipalToDependent.PropertyInfo;
                    shouldAuditOwnerProperty = IsAuditedPropertyInfo(ownerPropertyInfo);
                    if (shouldAuditOwnerProperty.HasValue && !shouldAuditOwnerProperty.Value)
                    {
                        continue;
                    }
                }

                var entityChange = CreateEntityChange(entityEntry);
                if (entityChange == null)
                {
                    continue;
                }

                var shouldSaveAuditedPropertiesOnly = !(shouldAuditEntity.HasValue || shouldAuditOwnerEntity.HasValue || shouldAuditOwnerProperty.HasValue) && 
                                                      !entityEntry.IsCreated() &&
                                                      !entityEntry.IsDeleted();
                var propertyChanges = GetPropertyChanges(entityEntry, shouldSaveAuditedPropertiesOnly);
                if (propertyChanges.Count == 0)
                {
                    continue;
                }

                entityChange.PropertyChanges = propertyChanges;
                changeSet.EntityChanges.Add(entityChange);
            }

            return changeSet;
        }

        public virtual async Task SaveAsync(EntityChangeSet changeSet)
        {
            if (!IsEntityHistoryEnabled)
            {
                return;
            }

            if (changeSet.EntityChanges.Count == 0)
            {
                return;
            }

            UpdateChangeSet(changeSet);

            using (var uow = UnitOfWorkManager.Begin(TransactionScopeOption.Suppress))
            {
                await EntityHistoryStore.SaveAsync(changeSet);
                await uow.CompleteAsync();
            }
        }

        protected virtual string GetEntityId(EntityEntry entry)
        {
            var primaryKeys = entry.Properties.Where(p => p.Metadata.IsPrimaryKey());
            return primaryKeys.First().CurrentValue?.ToJsonString();
        }

        public virtual void Save(EntityChangeSet changeSet)
        {
            if (!IsEntityHistoryEnabled)
            {
                return;
            }

            if (changeSet.EntityChanges.Count == 0)
            {
                return;
            }

            UpdateChangeSet(changeSet);

            using (var uow = UnitOfWorkManager.Begin(TransactionScopeOption.Suppress))
            {
                EntityHistoryStore.Save(changeSet);
                uow.Complete();
            }
        }

        [CanBeNull]
        private EntityChange CreateEntityChange(EntityEntry entityEntry)
        {
            EntityChangeType changeType;
            switch (entityEntry.State)
            {
                case EntityState.Added:
                    changeType = EntityChangeType.Created;
                    break;
                case EntityState.Deleted:
                    changeType = EntityChangeType.Deleted;
                    break;
                case EntityState.Modified:
                    changeType = entityEntry.IsDeleted() ? EntityChangeType.Deleted : EntityChangeType.Updated;
                    break;
                case EntityState.Detached:
                case EntityState.Unchanged:
                default:
                    Logger.ErrorFormat("Unexpected {0} - {1}", nameof(entityEntry.State), entityEntry.State);
                    return null;
            }

            var entityId = GetEntityId(entityEntry);
            if (entityId == null && changeType != EntityChangeType.Created)
            {
                Logger.ErrorFormat("EntityChangeType {0} must have non-empty entity id", changeType);
                return null;
            }

            return new EntityChange
            {
                ChangeType = changeType,
                EntityEntry = entityEntry, // [NotMapped]
                EntityId = entityId,
                EntityTypeFullName = entityEntry.Entity.GetType().FullName,
                TenantId = AbpSession.TenantId
            };
        }

        /// <summary>
        /// Gets the property changes for this entry.
        /// </summary>
        private ICollection<EntityPropertyChange> GetPropertyChanges(EntityEntry entityEntry, bool auditedPropertiesOnly)
        {
            var propertyChanges = new List<EntityPropertyChange>();
            var properties = entityEntry.Metadata.GetProperties();

            foreach (var property in properties)
            {
                if (property.IsPrimaryKey())
                {
                    continue;
                }

                var shouldSaveProperty = property.IsShadowProperty ||
                                         (IsAuditedPropertyInfo(property.PropertyInfo) ?? !auditedPropertiesOnly);
                if (shouldSaveProperty)
                {
                    var propertyEntry = entityEntry.Property(property.Name);
                    propertyChanges.Add(
                        CreateEntityPropertyChange(
                            propertyEntry.GetOriginalValue(),
                            propertyEntry.GetNewValue(),
                            property
                        )
                    );
                }
            }

            return propertyChanges;
        }

        /// <summary>
        /// Updates change time, entity id and foreign keys after SaveChanges is called.
        /// </summary>
        private void UpdateChangeSet(EntityChangeSet changeSet)
        {
            foreach (var entityChange in changeSet.EntityChanges)
            {
                var entityEntry = entityChange.EntityEntry.As<EntityEntry>();

                /* Update change time */
                entityChange.ChangeTime = GetChangeTime(entityChange.ChangeType, entityEntry.Entity);

                /* Update entity id */
                entityChange.EntityId = GetEntityId(entityEntry);

                /* Update foreign keys */

                var foreignKeys = entityEntry.Metadata.GetForeignKeys();

                foreach (var foreignKey in foreignKeys)
                {
                    foreach (var property in foreignKey.Properties)
                    {
                        var propertyEntry = entityEntry.Property(property.Name);
                        var propertyChange = entityChange.PropertyChanges.FirstOrDefault(pc => pc.PropertyName == property.Name);

                        if (propertyChange == null)
                        {
                            if (!(propertyEntry.OriginalValue?.Equals(propertyEntry.CurrentValue) ?? propertyEntry.CurrentValue == null))
                            {
                                // Add foreign key
                                entityChange.PropertyChanges.Add(
                                    CreateEntityPropertyChange(
                                        propertyEntry.GetOriginalValue(),
                                        propertyEntry.GetNewValue(),
                                        property
                                ));
                            }

                            continue;
                        }

                        if (propertyChange.OriginalValue == propertyChange.NewValue)
                        {
                            var newValue = propertyEntry.CurrentValue.ToJsonString();
                            if (newValue == propertyChange.NewValue)
                            {
                                // No change
                                entityChange.PropertyChanges.Remove(propertyChange);
                            }
                            else
                            {
                                // Update foreign key
                                propertyChange.NewValue = newValue.TruncateWithPostfix(EntityPropertyChange.MaxValueLength);
                            }
                        }
                    }
                }
            }
        }

        private EntityPropertyChange CreateEntityPropertyChange(object oldValue, object newValue, IProperty property)
        {
            return new EntityPropertyChange()
            {
                OriginalValue = oldValue?.ToJsonString().TruncateWithPostfix(EntityPropertyChange.MaxValueLength),
                NewValue = newValue?.ToJsonString().TruncateWithPostfix(EntityPropertyChange.MaxValueLength),
                PropertyName = property.Name.TruncateWithPostfix(EntityPropertyChange.MaxPropertyNameLength),
                PropertyTypeFullName = property.ClrType.FullName.TruncateWithPostfix(EntityPropertyChange.MaxPropertyTypeFullNameLength),
                TenantId = AbpSession.TenantId
            };
        }
    }
}
