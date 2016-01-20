﻿using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Abp.Dependency;
using Abp.Domain.Uow;
using Abp.Runtime.Serialization;
using Abp.Threading;
using Abp.Threading.BackgroundWorkers;
using Abp.Threading.Timers;
using Abp.Timing;
using Castle.Core.Logging;

namespace Abp.BackgroundJobs
{
    public class BackgroundJobManager : BackgroundWorkerBase, IBackgroundJobManager
    {
        public ILogger Logger { get; set; }

        private readonly IIocResolver _iocResolver;
        private readonly IBackgroundJobStore _store;
        private readonly AbpTimer _timer;

        public BackgroundJobManager(
            IIocResolver iocResolver,
            IBackgroundJobStore store, 
            AbpTimer timer)
        {
            _store = store;
            _timer = timer;
            _iocResolver = iocResolver;

            _timer.Period = 5000; //5 seconds
            _timer.Elapsed += Timer_Elapsed;
        }

        public async Task EnqueueAsync<TJob>(object state, BackgroundJobPriority priority = BackgroundJobPriority.Normal, TimeSpan? delay = null)
            where TJob : IBackgroundJob
        {
            var jobInfo = new BackgroundJobInfo
            {
                JobType = typeof(TJob).AssemblyQualifiedName,
                State = BinarySerializationHelper.Serialize(state),
                Priority = priority
            };

            if (delay.HasValue)
            {
                jobInfo.NextTryTime = Clock.Now.Add(delay.Value);
            }

            await _store.InsertAsync(jobInfo);
        }

        private void Timer_Elapsed(object sender, EventArgs e)
        {
            try
            {
                foreach (var task in AsyncHelper.RunSync(GetWaitingTasksAsync))
                {
                    TryProcessTask(task);
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex.Message, ex);
            }
        }

        private void TryProcessTask(BackgroundJobInfo jobInfo)
        {
            try
            {
                jobInfo.TryCount++;
                jobInfo.LastTryTime = DateTime.Now;

                var jobType = Type.GetType(jobInfo.JobType);
                using (var job = _iocResolver.ResolveAsDisposable<IBackgroundJob>(jobType))
                {
                    var stateObj = BinarySerializationHelper.DeserializeExtended(jobInfo.State);

                    try
                    {
                        job.Object.Execute(stateObj);
                        AsyncHelper.RunSync(() => _store.DeleteAsync(jobInfo));
                    }
                    catch (Exception ex)
                    {
                        Logger.Warn(ex.Message, ex);

                        var nextTryTime = jobInfo.CalculateNextTryTime();
                        if (nextTryTime.HasValue)
                        {
                            jobInfo.NextTryTime = nextTryTime.Value;
                        }
                        else
                        {
                            jobInfo.IsAbandoned = true;
                        }

                        try
                        {
                            _store.UpdateAsync(jobInfo);
                        }
                        catch (Exception updateEx)
                        {
                            Logger.Warn(updateEx.ToString(), updateEx);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Warn(ex.ToString(), ex);

                jobInfo.IsAbandoned = true;

                try
                {
                    _store.UpdateAsync(jobInfo);
                }
                catch (Exception updateEx)
                {
                    Logger.Warn(updateEx.ToString(), updateEx);
                }
            }
        }

        [UnitOfWork]
        protected virtual Task<List<BackgroundJobInfo>> GetWaitingTasksAsync()
        {
            return _store.GetWaitingJobsAsync(1000);
        }
    }
}
