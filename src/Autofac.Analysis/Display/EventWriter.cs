using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Autofac.Analysis.Engine.Analytics;
using Autofac.Analysis.Engine.Application;
using Autofac.Analysis.Engine.Session;
using Serilog;
using Serilog.Events;

namespace Autofac.Analysis.Display
{
    internal sealed class EventWriter :
        IApplicationEventHandler<MessageEvent>,
        IApplicationEventHandler<ItemCreatedEvent<ResolveOperation>>,
        IApplicationEventHandler<ItemCompletedEvent<ResolveOperation>>,
        IDisposable,
        IStartable
    {
        private readonly IApplicationEventBus _eventBus;
        private readonly ILogger _logger;

        public EventWriter(IApplicationEventBus eventBus, ILogger logger)
        {
            if (eventBus == null) throw new ArgumentNullException("eventBus");
            _eventBus = eventBus;
            _logger = logger.ForContext<EventWriter>();
        }

        public void Start()
        {
            _eventBus.Subscribe(this);
        }

        public void Handle(MessageEvent applicationEvent)
        {
            var level = LogEventLevel.Information;
            if (applicationEvent.Relevance == MessageRelevance.Error)
                level = LogEventLevel.Error;
            else if (applicationEvent.Relevance == MessageRelevance.Warning)
                level = LogEventLevel.Warning;

            _logger.Write(level, "{Title}: {Message}", applicationEvent.Title, applicationEvent.Message);
        }

        public void Dispose()
        {
            _eventBus.Unsubscribe(this);
        }

        public void Handle(ItemCreatedEvent<ResolveOperation> applicationEvent)
        {
            _logger.Information("Resolve operation {ResolveOperationId} started", applicationEvent.Item.Id);
        }

        public void Handle(ItemCompletedEvent<ResolveOperation> applicationEvent)
        {
            // There's a pile of data on the item here that needs to be meaninfully output (e.g. the
            // whole resolved object graph shape and what was created where).
            //_logger.Information("Resolve operation {ResolveOperationId} completed", applicationEvent.Item.Id);

            LogResolvedObjectGraph(applicationEvent.Item);
            _logger.Information("Resolve operation {ResolveOperationId} completed", applicationEvent.Item.Id);

        }

        private void LogResolvedObjectGraph(ResolveOperation resolution)
        {
            var root = resolution.RootInstanceLookup;
            _logger.Debug("Resolved {Component} from {Source}", root.Component.Description, resolution.CallingMethod.DisplayName);
            if (!root.DependencyLookups.Any())
            {
                _logger.Debug("{Component} has no dependencies", root.Component.Description);
            }
            else
            {
                LogInstanceLookup(root.DependencyLookups.First().Dependent, 0);
            }
        }

        private void LogInstanceLookup(InstanceLookup instance, int depth)
        {
            if (!instance.DependencyLookups.Any()) return;

            _logger.Debug("{Prefix} {Component} depends on:", GetPrefix(depth), instance.Component.Description);

            foreach (var dep in instance.DependencyLookups)
            {
                _logger.Debug("{Prefix} {Component}", GetPrefix(depth + 1), dep.Component.Description);
                LogInstanceLookup(dep, depth + 2);
            }
        }

        private static string GetPrefix(int depth)
        {
            return new string('-', depth*2);
        }
    }
}