// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 08/17/2026

namespace WolfCurses.Games.Battlezone
{
    /// <summary>
    ///     A block or a pyramid standing on the plain. It stops tanks and it stops shells, which is what makes the
    ///     scenery tactical rather than decorative — there is somewhere to hide, and so somewhere to be hidden from.
    /// </summary>
    public sealed class Obstacle
    {
        /// <summary>Initializes a new instance of the <see cref="Obstacle" /> class.</summary>
        /// <param name="x">Where it stands, east.</param>
        /// <param name="z">Where it stands, north.</param>
        /// <param name="kind">What it looks like.</param>
        public Obstacle(double x, double z, ObstacleKindEnum kind)
        {
            X = x;
            Z = z;
            Kind = kind;
        }

        /// <summary>Where it stands, east. Settable because the scenery is recycled — see <see cref="BattleField" />.</summary>
        public double X { get; internal set; }

        /// <summary>Where it stands, north.</summary>
        public double Z { get; internal set; }

        /// <summary>What it looks like.</summary>
        public ObstacleKindEnum Kind { get; internal set; }

        /// <summary>
        ///     How far its footprint reaches, for stopping tanks and shells — and, because the models are authored
        ///     in a unit box, the scale it is drawn at. One number rather than two: a block whose picture and whose
        ///     footprint could disagree is one a player can be shot through.
        /// </summary>
        public double Radius => Kind == ObstacleKindEnum.Cube ? 9.0 : 8.0;
    }
}
