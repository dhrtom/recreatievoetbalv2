﻿using System;
using System.Threading;
using System.Threading.Tasks;
using Lib.Net.Http.WebPush;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Services.Abstractions;

namespace Services
{
    internal class PushNotificationsDequeuer : IHostedService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly IPushNotificationsQueue _messagesQueue;
        private readonly IPushNotificationService _notificationService;
        private readonly CancellationTokenSource _stopTokenSource = new CancellationTokenSource();

        private Task _dequeueMessagesTask;

        public PushNotificationsDequeuer(IServiceProvider serviceProvider, IPushNotificationsQueue messagesQueue, IPushNotificationService notificationService)
        {
            _serviceProvider = serviceProvider;
            _messagesQueue = messagesQueue;
            _notificationService = notificationService;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _dequeueMessagesTask = Task.Run(DequeueMessagesAsync);

            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _stopTokenSource.Cancel();
            if (_dequeueMessagesTask != null)
            {
                return Task.WhenAny(_dequeueMessagesTask, Task.Delay(Timeout.Infinite, cancellationToken));
            }

            return Task.CompletedTask;
        }

        private async Task DequeueMessagesAsync()
        {
            while (!_stopTokenSource.IsCancellationRequested)
            {
                PushMessage message = await _messagesQueue.DequeueAsync(_stopTokenSource.Token);

                if (!_stopTokenSource.IsCancellationRequested)
                {
                    using (IServiceScope serviceScope = _serviceProvider.CreateScope())
                    {
                        IPushSubscriptionStore subscriptionStore = serviceScope.ServiceProvider.GetRequiredService<IPushSubscriptionStore>();

                        await subscriptionStore.ForEachSubscriptionAsync((PushSubscription subscription) =>
                        {
                            // Fire-and-forget 
                            _notificationService.SendNotificationAsync(subscription, message, _stopTokenSource.Token);
                                         }, _stopTokenSource.Token);
                                     }
                 
                                 }
            }

        }
    }
}
