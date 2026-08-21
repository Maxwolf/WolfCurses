// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 08/21/2026

namespace WolfCurses.Apps.Basic
{
    /// <summary>
    ///     A jump, either always or on a condition. Every block construct in the language compiles down to these,
    ///     which is why there is no IF statement type and no WHILE statement type.
    ///     <para>
    ///         <b>The target is settable</b>, because a forward jump is emitted before anybody knows where it goes:
    ///         the parser emits it with no target, carries on, and patches it when it reaches the END IF. That is
    ///         the standard trick and it is the reason this is a class rather than something immutable.
    ///     </para>
    /// </summary>
    public sealed class BasicJumpStatement : BasicStatement
    {
        /// <summary>The condition, or null to jump always.</summary>
        private readonly BasicExpression _condition;

        /// <summary>Whether to jump when the condition is true, or when it is false.</summary>
        private readonly bool _jumpWhenTrue;

        /// <summary>Initializes a new instance of the <see cref="BasicJumpStatement" /> class.</summary>
        /// <param name="condition">The condition, or null to jump always.</param>
        /// <param name="jumpWhenTrue">TRUE to jump when the condition holds.</param>
        /// <param name="line">The source line.</param>
        public BasicJumpStatement(BasicExpression condition, bool jumpWhenTrue, int line) : base(line)
        {
            _condition = condition;
            _jumpWhenTrue = jumpWhenTrue;
            Target = -1;
        }

        /// <summary>Where to jump to, patched by the parser once it knows.</summary>
        public int Target { get; set; }

        /// <inheritdoc />
        public override int Execute(BasicRuntime runtime, int index)
        {
            if (_condition == null)
                return Target;

            return _condition.Evaluate(runtime).IsTrue == _jumpWhenTrue ? Target : index + 1;
        }
    }
}
