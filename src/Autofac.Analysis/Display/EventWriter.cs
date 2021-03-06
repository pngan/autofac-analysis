﻿using System;
using System.Linq;
using Autofac.Analysis.Engine.Analytics;
using Autofac.Analysis.Engine.Application;
using Autofac.Analysis.Engine.Session;
using Serilog;

namespace Autofac.Analysis.Display
{
    sealed class EventWriter :
        IApplicationEventHandler<MessageEvent>,
        IApplicationEventHandler<ItemCreatedEvent<ResolveOperation>>,
        IApplicationEventHandler<ItemCompletedEvent<ResolveOperation>>,
        IDisposable,
        IStartable
    {
        readonly IApplicationEventBus _eventBus;
        readonly ILogger _logger;

        public EventWriter(IApplicationEventBus eventBus)
        {
            if (eventBus == null) throw new ArgumentNullException("eventBus");
            _eventBus = eventBus;
            _logger = Log.ForContext<EventWriter>();
        }

        public void Start()
        {
            _eventBus.Subscribe(this);
        }

        public void Handle(MessageEvent applicationEvent)
        {
            _logger.Write(applicationEvent.Level, applicationEvent.MessageTemplate, applicationEvent.Args);
        }

        public void Dispose()
        {
            _eventBus.Unsubscribe(this);
        }

        public void Handle(ItemCreatedEvent<ResolveOperation> applicationEvent)
        {
            if (applicationEvent.Item.Parent != null)
            {
                _logger
                    .ForContext("ResolveOperationId", applicationEvent.Item.Id)
                    .ForContext("ParentResolveOperationId", applicationEvent.Item.Parent.Id)
                    .Information("Resolve operation {ResolveOperationShortId} started as child of {ParentResolveOperationShortId}",
                        IdDisplay.MakeShortId(applicationEvent.Item.Id), IdDisplay.MakeShortId(applicationEvent.Item.Parent.Id));
            }
            else
            {
                _logger
                    .ForContext("ResolveOperationId", applicationEvent.Item.Id)
                    .Information("Resolve operation {ResolveOperationShortId} started from {CallingMethod}", IdDisplay.MakeShortId(applicationEvent.Item.Id), applicationEvent.Item.CallingMethod.DisplayName);
            }
        }

        public void Handle(ItemCompletedEvent<ResolveOperation> applicationEvent)
        {
            // There's a pile of data on the item here that needs to be meaninfully output (e.g. the
            // whole resolved object graph shape and what was created where).
            //
            var graph = ToObjectGraph(applicationEvent.Item.RootInstanceLookup);

            _logger
                .ForContext("ResolveOperationId", applicationEvent.Item.Id)
                .Information("Resolve operation {ResolveOperationShortId} returned {@Graph}", IdDisplay.MakeShortId(applicationEvent.Item.Id), graph);
        }

        static object ToObjectGraph(InstanceLookup instanceLookup)
        {
            if (instanceLookup == null) throw new ArgumentNullException("instanceLookup");

            // Just hacking things to keep clutter down, needs cleaning up

            if (instanceLookup.SharedInstanceReused)
            {
                if (instanceLookup.DependencyLookups.Count == 0)
                {
                    return new
                    {
                        Component = instanceLookup.Component.LimitType.Identity.DisplayName,
                        Reused = instanceLookup.SharedInstanceReused,
                        Scope = instanceLookup.ActivationScope.Description
                    };
                }

                return new
                {
                    Component = instanceLookup.Component.LimitType.Identity.DisplayName,
                    Reused = instanceLookup.SharedInstanceReused,
                    Scope = instanceLookup.ActivationScope.Description,
                    Dependencies = instanceLookup.DependencyLookups.Select(ToObjectGraph).ToArray()
                };
            }

            if (instanceLookup.DependencyLookups.Count == 0)
            {
                return new
                {
                    Component = instanceLookup.Component.LimitType.Identity.DisplayName,
                    Scope = instanceLookup.ActivationScope.Description
                };
            }

            return new
            {
                Component = instanceLookup.Component.LimitType.Identity.DisplayName,
                Scope = instanceLookup.ActivationScope.Description,
                Dependencies = instanceLookup.DependencyLookups.Select(ToObjectGraph).ToArray()
            };
        }
    }
}
