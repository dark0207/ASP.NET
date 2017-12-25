﻿using System.Threading.Tasks;

namespace Abp.EntityHistory
{
    /// <summary>
    /// This interface should be implemented by vendors to
    /// make entity history working.
    /// </summary>
    public interface IEntityHistoryStore
    {
        /// <summary>
        /// Should save entity changes to a persistent store.
        /// </summary>
        /// <param name="entityChangeInfo">Entity change informations</param>
        Task SaveAsync(EntityChangeInfo entityChangeInfo);

        /// <summary>
        /// Should save entity change set to a persistent store.
        /// </summary>
        /// <param name="entityChangeSet">Entity change set</param>
        Task SaveAsync(EntityChangeSet entityChangeSet);
    }
}
