﻿using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Abp.Domain.Entities;
using Abp.EntityFramework;

namespace Abp.EntityHistory
{
    public interface IEntitySnapshotManager
    {
        
        /// <summary>
        /// Returns snapshot of entity at given snapshotTime.
        /// <para>
        /// The ChangedPropertiesSnapshots values represent the value of the entity at snapShotTime.
        /// The PropertyChangesStackTree values represent the stacktree of changed value from now to snapshotTime.
        /// </para>
        /// </summary>
        /// <typeparam name="TEntity">typeof entity</typeparam>
        /// <typeparam name="TPrimaryKey">typeof primary key</typeparam>
        /// <param name="id">entity id</param>
        /// <param name="snapshotTime"></param>
        Task<EntityHistorySnapshot> GetSnapshotAsync<TEntity, TPrimaryKey>(TPrimaryKey id, DateTime snapshotTime) where TEntity : class, IEntity<TPrimaryKey>;

        /// <summary>
        /// shortcut of GetEntitySnapshotAsync &lt;TEntity, int &gt;
        /// </summary>
        Task<EntityHistorySnapshot> GetSnapshotAsync<TEntity>(int id, DateTime snapshotTime) where TEntity : class, IEntity<int>;
    }
}
