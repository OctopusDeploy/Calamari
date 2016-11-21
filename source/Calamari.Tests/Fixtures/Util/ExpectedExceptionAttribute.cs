using System;
using NUnit.Framework;
using NUnit.Framework.Interfaces;
using NUnit.Framework.Internal;
using NUnit.Framework.Internal.Commands;

namespace Calamari.Tests.Fixtures.Util
{
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
    public class ExpectedExceptionAttribute : NUnitAttribute, IWrapTestMethod
    {
        private readonly Type _expectedExceptionType;
        public string ExpectedMessage;
        public MessageMatch MatchType;

        public ExpectedExceptionAttribute(Type type = null)
        {
            _expectedExceptionType = type;
        }

        public TestCommand Wrap(TestCommand command)
        {
            return new ExpectedExceptionCommand(command, _expectedExceptionType, ExpectedMessage, MatchType);
        }

        private class ExpectedExceptionCommand : DelegatingTestCommand
        {
            private readonly Type _expectedType;
            private readonly string _expectedMessage;
            private readonly MessageMatch _matchType;

            public ExpectedExceptionCommand(TestCommand innerCommand, Type expectedType, string expectedMessage, MessageMatch matchType)
                : base(innerCommand)
            {
                _expectedType = expectedType;
                _expectedMessage = expectedMessage;
                _matchType = matchType;
            }

            public override TestResult Execute(TestExecutionContext context)
            {
                Type caughtType = null;
                string message = null;

                try
                {
                    innerCommand.Execute(context);
                }
                catch (Exception ex)
                {
                    if (ex is NUnitException)
                        ex = ex.InnerException;
                    caughtType = ex.GetType();
                    message = ex.Message;
                }

                var expectedTypeName = _expectedType == null ? "an exception" : _expectedType.Name;

                if ((_expectedType != null && caughtType == _expectedType) || (_expectedType == null && caughtType != null))
                {
                    if (MessageMatches(message))
                    {
                        context.CurrentResult.SetResult(ResultState.Success);
                    }
                    else
                    {
                        string.Format("Expected message to {0} {1} but got {2}", GetMatchText(), _expectedMessage, message);
                    }
                }
                else if (caughtType != null)
                    context.CurrentResult.SetResult(ResultState.Failure,
                        string.Format("Expected {0} but got {1}", expectedTypeName, caughtType.Name));
                else
                    context.CurrentResult.SetResult(ResultState.Failure,
                        string.Format("Expected {0} but no exception was thrown", expectedTypeName));

                return context.CurrentResult;
            }

            private string GetMatchText()
            {
                switch (_matchType)
                {
                    case MessageMatch.Equals:
                        return "be";
                    case MessageMatch.StartsWith:
                        return "start with";
                    case MessageMatch.EndsWith:
                        return "end with";
                    case MessageMatch.Contains:
                        return "contain";
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }

            private bool MessageMatches(string message)
            {
                if (_expectedMessage == null)
                    return true;

                switch (_matchType)
                {
                    case MessageMatch.Equals:
                        return _expectedMessage == message;
                    case MessageMatch.StartsWith:
                        return _expectedMessage.StartsWith(message);
                    case MessageMatch.EndsWith:
                        return _expectedMessage.EndsWith(message);
                    case MessageMatch.Contains:
                        return _expectedMessage.Contains(message);
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
        }
    }

    public enum MessageMatch
    {
        Equals,
        StartsWith,
        EndsWith,
        Contains
    }
}