﻿using System.Diagnostics.Tracing;

namespace ChilliCream.Logging.Analyzer.Tests.EventSources
{
    public sealed class ConstructionExceptionEventSource
        : EventSource
    {
        private ConstructionExceptionEventSource()
            : base ("ConstructionException", EventSourceSettings.Default, new [] { "odd", "even", "odd" })
        { }

        public static ConstructionExceptionEventSource Log = new ConstructionExceptionEventSource();

        [Event(1)]
        public void Foo(string bar)
        {
            WriteEvent(1, bar, "sdfsdfs");
        }
    }
}