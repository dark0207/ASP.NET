﻿using System.Threading.Tasks;

namespace Abp.Notifications
{
    /// <summary>
    /// Main service to subscribe to and publish notifications.
    /// </summary>
    public interface INotificationManager
    {
        Task SubscribeAsync(NotificationSubscriptionOptions options);

        //TODO: Unsubscribe

        Task PublishAsync(NotificationPublishOptions options);
    }
}
