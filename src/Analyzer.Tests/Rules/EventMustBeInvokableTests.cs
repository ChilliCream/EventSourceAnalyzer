﻿using Thor.Analyzer.Rules;
using Thor.Analyzer.Tests.EventSources;
using FluentAssertions;
using Moq;
using System.Linq;
using Xunit;

namespace Thor.Analyzer.Tests.Rules
{
    public class EventMustBeInvokableTests
        : EventRuleTestBase<EventMustBeInvokable>
    {
        protected override EventMustBeInvokable CreateRule(IRuleSet ruleSet)
        {
            return new EventMustBeInvokable(ruleSet);
        }

        [Fact(DisplayName = "Apply: Should return an error if events were not invokable")]
        public void Apply_Error()
        {
            // arrange
            EventNotWorkingEventSource eventSource = EventNotWorkingEventSource.Log;
            SchemaReader reader = new SchemaReader(eventSource);
            EventSourceSchema schema = reader.Read();
            IRuleSet ruleSet = new Mock<IRuleSet>().Object;
            IEventRule rule = CreateRule(ruleSet);

            // act
            IResult result = rule.Apply(schema.Events.First(), eventSource);

            // assert
            result.Should().NotBeNull();
            result.Should().BeOfType<Error>();
            ((Error)result).Details.Should().HaveCount(0);
        }

        [Fact(DisplayName = "Apply: Should return a success if events were invokable")]
        public void Apply_Success()
        {
            // arrange
            EventWorkingEventSource eventSource = EventWorkingEventSource.Log;
            SchemaReader reader = new SchemaReader(eventSource);
            EventSourceSchema schema = reader.Read();
            IRuleSet ruleSet = new Mock<IRuleSet>().Object;
            IEventRule rule = CreateRule(ruleSet);

            // act
            IResult result = rule.Apply(schema.Events.First(), eventSource);

            // assert
            result.Should().NotBeNull();
            result.Should().BeOfType<Success>();
        }
    }
}