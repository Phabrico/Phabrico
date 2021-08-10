using Phabrico.Http;
using System;
using System.Collections.Generic;

namespace Phabrico.Parsers.Remarkup.Rules
{
    /// <summary>
    /// Abstract class for Remarkup rules
    /// </summary>
    public abstract class RemarkupRule
    {
        /// <summary>
        /// Decoded HTML
        /// </summary>
        public string Html { get; set; }

        /// <summary>
        /// Length of current RemarkupRule
        /// </summary>
        public int Length { get; set; }

        /// <summary>
        /// Start position where current RemarkupRule is positioned
        /// </summary>
        public int Start { get; set; }

        /// <summary>
        /// Content of RemarkupRule
        /// </summary>
        public string Text { get; set; }

        /// <summary>
        /// Determines the process order of the rule.
        /// A low priority number means a earlier rule processing
        /// </summary>
        [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
        public class RulePriority : Attribute
        {
            /// <summary>
            /// Default priority for all RemarkupRules
            /// </summary>
            public const int DefaultPriority = 50;

            /// <summary>
            /// Priority value
            /// </summary>
            public int Priority { get; private set; } = 0;

            /// <summary>
            /// Initializes a newRulePriority instance
            /// </summary>
            /// <param name="priority"></param>
            public RulePriority(int priority)
            {
                Priority = priority;
            }
        }

        /// <summary>
        /// This attribute disallows the innertext to be decoded as Remarkup for the given RuleType
        /// </summary>
        [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
        public class RuleNotInnerRuleFor : Attribute
        {
            /// <summary>
            /// Defines the outer Rule type where the current Rule is located in
            /// </summary>
            public Type ParentRuleType { get; private set; }

            /// <summary>
            /// Initializes a new instance of RuleNotInnerRuleFor
            /// </summary>
            /// <param name="parentRuleType"></param>
            public RuleNotInnerRuleFor(Type parentRuleType)
            {
                ParentRuleType = parentRuleType;
            }
        }

        /// <summary>
        /// If true, the current rule will only be valid if it starts on a new line
        /// </summary>
        public bool RuleStartOnNewLine { get; set; } = true;

        /// <summary>
        /// If true, the current rule will only be valid if a whitespace precedes it
        /// </summary>
        public bool RuleStartAfterWhiteSpace { get; set; } = true;

        /// <summary>
        /// Reference to the Remarkup engine
        /// </summary>
        public RemarkupEngine Engine { get; set; } = null;

        /// <summary>
        /// Collection of all phabricator objects (e.g phriction, maniphest, ...) that are referenced in the current remarkup rule
        /// </summary>
        public List<Phabricator.Data.PhabricatorObject> LinkedPhabricatorObjects = new List<Phabricator.Data.PhabricatorObject>();

        /// <summary>
        /// Some Remarkup rules can be executed in another Remarkup rule (e.g. bold text in a table cell)
        /// This property represents the outer Remarkup rule
        /// </summary>
        public RemarkupRule ParentRemarkupRule { get; set; }

        /// <summary>
        /// List of processed tokens
        /// </summary>
        public List<Rules.RemarkupRule> TokenList { get; set; }

        /// <summary>
        /// Creates a copy of the current RemarkupRule
        /// </summary>
        /// <returns></returns>
        public RemarkupRule Clone()
        {
            RemarkupRule clonedRemarkupRule = GetType().GetConstructor(Type.EmptyTypes).Invoke(null) as RemarkupRule;

            clonedRemarkupRule.Html = Html;
            clonedRemarkupRule.Length = Length;
            clonedRemarkupRule.Start = Start;
            clonedRemarkupRule.Text = Text;
            clonedRemarkupRule.Clone(this);
            clonedRemarkupRule.LinkedPhabricatorObjects.AddRange(LinkedPhabricatorObjects);

            return clonedRemarkupRule;
        }

        /// <summary>
        /// Virtual method which allows to do a deeper clone of RemarkupRule
        /// </summary>
        /// <param name="originalRemarkupRule"></param>
        public virtual void Clone(RemarkupRule originalRemarkupRule)
        {
        }

        /// <summary>
        /// This method is executed before the ToHTML() is executed.
        /// It is meant for initializing some local variables
        /// </summary>
        public virtual void Initialize()
        {
        }

        /// <summary>
        /// Converts Remarkup encoded text into HTML
        /// </summary>
        /// <param name="database">Reference to Phabrico database</param>
        /// <param name="browser">Reference to web browser</param>
        /// <param name="url">URL from where this method was executed</param>
        /// <param name="remarkup">Remarkup content to be converted</param>
        /// <param name="html">Translated HTML</param>
        /// <returns>True if the content could successfully be converted</returns>
        public abstract bool ToHTML(Storage.Database database, Browser browser, string url, ref string remarkup, out string html);

        /// <summary>
        /// Returns a readable description
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            return string.Format("{{{0}}}: \"{1}\"", GetType().Name, Text);
        }
    }
}
