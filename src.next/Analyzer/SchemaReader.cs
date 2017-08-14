﻿using System;
using System.Collections.Immutable;
using System.Diagnostics.Tracing;
using System.Linq;
using System.Xml.Linq;

namespace ChilliCream.Logging.Analyzer
{
    /// <summary>
    /// Parses the ETW manifest generated by the <see cref="EventSource"/> class.
    /// </summary>
    public class SchemaReader
    {
        #region XML definitions

        private static readonly XNamespace _ns = "http://schemas.microsoft.com/win/2004/08/events";
        private static readonly XName _root = _ns + "instrumentationManifest";
        private static readonly XName _instrumentation = _ns + "instrumentation";
        private static readonly XName _events = _ns + "events";
        private static readonly XName _provider = _ns + "provider";
        private static readonly XName _tasks = _ns + "tasks";
        private static readonly XName _task = _ns + "task";
        private static readonly XName _keywords = _ns + "keywords";
        private static readonly XName _keyword = _ns + "keyword";
        private static readonly XName _opcodes = _ns + "opcodes";
        private static readonly XName _opcode = _ns + "opcode";
        private static readonly XName _event = _ns + "event";
        private static readonly XName _templates = _ns + "templates";
        private static readonly XName _template = _ns + "template";

        #endregion

        private readonly EventSource _eventSource;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="eventSource"></param>
        public SchemaReader(EventSource eventSource)
        {
            if (eventSource == null)
            {
                throw new ArgumentNullException(nameof(eventSource));
            }

            _eventSource = eventSource;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public EventSourceSchema Read()
        {
            string manifest = EventSource.GenerateManifest(_eventSource.GetType(), null);

            return ParseSchema(manifest);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="eventId"></param>
        /// <returns></returns>
        public EventSchema ReadEvent(int eventId)
        {
            if (eventId < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(eventId),
                    ExceptionMessages.EventIdMustBeGreaterZero);
            }

            EventSourceSchema schema = Read();

            return schema.Events.FirstOrDefault(e => e.Id == eventId);
        }

        #region Schema parsing

        private static EventSourceSchema ParseSchema(string xmlManifest)
        {
            XDocument doc = XDocument.Parse(xmlManifest);
            XElement provider = doc.Root
                .Element(_instrumentation)
                .Element(_events)
                .Element(_provider);
            Guid providerGuid = (Guid)provider.Attribute("guid");
            string providerName = (string)provider.Attribute("name");
            EventSourceSchema eventSourceSchema = new EventSourceSchema(providerGuid, providerName);

            eventSourceSchema.Events = ParseEventSchemas(eventSourceSchema, provider);

            return eventSourceSchema;
        }

        private static ImmutableArray<EventSchema> ParseEventSchemas(EventSourceSchema eventSourceSchema, XElement providerElement)
        {
            return providerElement
                .Element(_events)
                .Elements(_event)
                .Where(e => (int)e.Attribute("value") > 0)
                .Select(e => CreateEventSchema(eventSourceSchema, e, providerElement))
                .ToImmutableArray<EventSchema>();
        }

        private static EventSchema CreateEventSchema(EventSourceSchema eventSourceSchema, XElement eventElement, XElement providerElement)
        {
            int eventId = (int)eventElement.Attribute("value");
            string eventName = (string)eventElement.Attribute("symbol");
            EventLevel level = ParseLevel((string)eventElement.Attribute("level"));
            int version = ParseVersion(eventElement.Attribute("version"));
            (string name, EventTask value) task = ParseTask(providerElement.Element(_tasks), eventElement);
            EventKeywords keywords = ParseKeywords(providerElement.Element(_keywords), eventElement);
            EventOpcode opcode = ParseOpcode((string)eventElement.Attribute("opcode"), providerElement.Element(_opcodes));
            ImmutableArray<string> payload = ParsePayload(providerElement.Element(_templates), eventElement);

            return new EventSchema(eventSourceSchema, eventId, eventName, level,
                task.value, task.name, opcode, keywords, version, payload);
        }

        private static int ParseVersion(XAttribute versionAttribute)
        {
            if (versionAttribute == null)
            {
                return 0;
            }

            return (int)versionAttribute;
        }

        private static EventKeywords ParseKeywords(XElement keywordsElement, XElement eventElement)
        {
            long keywordsMask = 0;
            string keywordNames = (string)eventElement.Attribute("keywords");

            if (!string.IsNullOrWhiteSpace(keywordNames))
            {
                foreach (string keywordName in keywordNames.Split())
                {
                    XAttribute keywordsMaskAttribute = keywordsElement
                        .Elements(_keyword)
                        .Where(k => (string)k.Attribute("name") == keywordName)
                        .Select(k => k.Attribute("mask"))
                        .FirstOrDefault();

                    if (keywordsMaskAttribute != null)
                    {
                        keywordsMask |= Convert.ToInt64(keywordsMaskAttribute.Value, 16);
                    }
                }
            }

            return (EventKeywords)keywordsMask;
        }

        private static (string name, EventTask task) ParseTask(XElement tasksElement, XElement eventElement)
        {
            string taskName = (string)eventElement.Attribute("task");
            int taskId = 0;

            if (!string.IsNullOrWhiteSpace(taskName))
            {
                taskId = (int)tasksElement
                    .Elements(_task)
                    .First(t => (string)t.Attribute("name") == taskName)
                    .Attribute("value");
            }

            return (taskName, (EventTask)taskId);
        }

        private static ImmutableArray<string> ParsePayload(XElement templatesElement, XElement eventElement)
        {
            XAttribute templateReferenceAttribute = eventElement.Attribute("template");

            if (templateReferenceAttribute == null)
            {
                return ImmutableArray<string>.Empty;
            }

            return templatesElement
                .Elements(_template)
                .First(t => (string)t.Attribute("tid") == templateReferenceAttribute.Value)
                .Elements(_ns + "data")
                .Select(d => (string)d.Attribute("name"))
                .ToImmutableArray<string>();
        }

        private static EventLevel ParseLevel(string level)
        {
            switch (level)
            {
                case "win:Critical":
                    return EventLevel.Critical;

                case "win:Error":
                    return EventLevel.Error;

                case "win:Warning":
                    return EventLevel.Warning;

                case "win:Informational":
                    return EventLevel.Informational;

                case "win:Verbose":
                    return EventLevel.Verbose;

                default:
                    return EventLevel.LogAlways;
            }
        }

        private static EventOpcode ParseOpcode(string opcodeName, XElement opcodesElement)
        {
            switch (opcodeName)
            {
                case null:
                case "win:Info":
                    return EventOpcode.Info;

                case "win:Start":
                    return EventOpcode.Start;

                case "win:Stop":
                    return EventOpcode.Stop;

                case "win:DC_Start":
                    return EventOpcode.DataCollectionStart;

                case "win:DC_Stop":
                    return EventOpcode.DataCollectionStop;

                case "win:Extension":
                    return EventOpcode.Extension;

                case "win:Reply":
                    return EventOpcode.Reply;

                case "win:Resume":
                    return EventOpcode.Resume;

                case "win:Suspend":
                    return EventOpcode.Suspend;

                case "win:Send":
                    return EventOpcode.Send;

                case "win:Receive":
                    return EventOpcode.Receive;
            }

            if (!string.IsNullOrWhiteSpace(opcodeName))
            {
                XElement opcodeElement = opcodesElement
                    .Elements(_opcode)
                    .FirstOrDefault(o => (string)o.Attribute("name") == opcodeName);

                if (opcodeElement != null)
                {
                    return (EventOpcode)(int)opcodeElement.Attribute("value");
                }
            }

            return EventOpcode.Info;
        }

        #endregion
    }
}