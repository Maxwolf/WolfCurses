// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 08/17/2026

using System.Numerics;

namespace WolfCurses.Games.Battlezone
{
    /// <summary>
    ///     A shape, as a list of corners and a list of which corners are joined to which.
    ///     <para>
    ///         <b>This is the entire art pipeline.</b> WolfChess 5000 loads twelve PNG files and composites them;
    ///         Missile Command draws circles and lines straight into a buffer; this game is the third answer, which
    ///         is that the artwork is a couple of dozen numbers in a source file and the picture is worked out from
    ///         where you happen to be standing. Nothing is copied beside the executable, and the same model draws
    ///         at any size and from any angle because it was never a picture in the first place.
    ///     </para>
    ///     <para>
    ///         Models are authored in a unit box — roughly -1 to 1 across, sitting on <c>y = 0</c>, facing
    ///         <c>+z</c> — and scaled when they are drawn, so the same cube serves a nine-unit block and a
    ///         nine-hundred-unit one. <see cref="Vector3" /> is the framework's own, which costs no package
    ///         reference; its <c>float</c> precision is irrelevant here because these are art, not physics, and the
    ///         world coordinates they are transformed into are doubles.
    ///     </para>
    /// </summary>
    public sealed class WireModel
    {
        private static WireModel _tank;
        private static WireModel _superTank;
        private static WireModel _saucer;
        private static WireModel _cube;
        private static WireModel _pyramid;
        private static WireModel _shell;

        /// <summary>Initializes a new instance of the <see cref="WireModel" /> class.</summary>
        /// <param name="vertices">The corners, in model space.</param>
        /// <param name="edges">Pairs of indexes into <paramref name="vertices" />, two per line.</param>
        public WireModel(Vector3[] vertices, int[] edges)
        {
            Vertices = vertices;
            Edges = edges;
        }

        /// <summary>The corners, in model space.</summary>
        public Vector3[] Vertices { get; }

        /// <summary>Pairs of indexes into <see cref="Vertices" />, two per line.</summary>
        public int[] Edges { get; }

        /// <summary>The enemy tank: a hull, a turret and a gun barrel that tells you where it is pointing.</summary>
        public static WireModel Tank => _tank ??= BuildTank(1.0f, 0.55f);

        /// <summary>The super tank: the same shape, squatter and wider, so the two read differently at range.</summary>
        public static WireModel SuperTank => _superTank ??= BuildTank(1.25f, 0.42f);

        /// <summary>The saucer: a rim with a dome above and a point below.</summary>
        public static WireModel Saucer => _saucer ??= BuildSaucer();

        /// <summary>A block.</summary>
        public static WireModel Cube => _cube ??= BuildCube();

        /// <summary>A pyramid.</summary>
        public static WireModel Pyramid => _pyramid ??= BuildPyramid();

        /// <summary>A shell in flight: three short strokes crossing, which is enough to be seen and no more.</summary>
        public static WireModel Shell => _shell ??= BuildShell();

        /// <summary>Whichever shape goes with a kind of enemy.</summary>
        /// <param name="kind">What it is.</param>
        /// <returns>The model to draw.</returns>
        public static WireModel For(EnemyKindEnum kind)
        {
            return kind switch
            {
                EnemyKindEnum.SuperTank => SuperTank,
                EnemyKindEnum.Saucer => Saucer,
                _ => Tank
            };
        }

        /// <summary>Whichever shape goes with a piece of scenery.</summary>
        /// <param name="kind">What it is.</param>
        /// <returns>The model to draw.</returns>
        public static WireModel For(ObstacleKindEnum kind)
        {
            return kind == ObstacleKindEnum.Pyramid ? Pyramid : Cube;
        }

        /// <summary>Builds a tank at a given width and height.</summary>
        /// <param name="width">How wide the hull is.</param>
        /// <param name="height">How tall the hull is.</param>
        /// <returns>The model.</returns>
        private static WireModel BuildTank(float width, float height)
        {
            var turret = width*0.5f;
            var vertices = new[]
            {
                // Hull, a box with a sloped nose - the slope is what makes it read as a tank rather than a crate.
                new Vector3(-width, 0f, -1.3f), new Vector3(width, 0f, -1.3f),
                new Vector3(width, 0f, 1.0f), new Vector3(-width, 0f, 1.0f),
                new Vector3(-width, height, -1.1f), new Vector3(width, height, -1.1f),
                new Vector3(width, height, 0.55f), new Vector3(-width, height, 0.55f),

                // Nose.
                new Vector3(-width*0.7f, height*0.35f, 1.5f), new Vector3(width*0.7f, height*0.35f, 1.5f),

                // Turret.
                new Vector3(-turret, height, -0.45f), new Vector3(turret, height, -0.45f),
                new Vector3(turret, height, 0.3f), new Vector3(-turret, height, 0.3f),
                new Vector3(-turret, height*1.75f, -0.35f), new Vector3(turret, height*1.75f, -0.35f),
                new Vector3(turret, height*1.75f, 0.2f), new Vector3(-turret, height*1.75f, 0.2f),

                // Barrel.
                new Vector3(0f, height*1.45f, 0.3f), new Vector3(0f, height*1.45f, 1.75f)
            };

            var edges = new[]
            {
                0, 1, 1, 2, 2, 3, 3, 0,
                4, 5, 5, 6, 6, 7, 7, 4,
                0, 4, 1, 5, 2, 6, 3, 7,
                2, 9, 3, 8, 8, 9, 6, 9, 7, 8,
                10, 11, 11, 12, 12, 13, 13, 10,
                14, 15, 15, 16, 16, 17, 17, 14,
                10, 14, 11, 15, 12, 16, 13, 17,
                18, 19
            };

            return new WireModel(vertices, edges);
        }

        /// <summary>Builds the saucer: a hexagonal rim, a point above and a point below.</summary>
        /// <returns>The model.</returns>
        private static WireModel BuildSaucer()
        {
            const int sides = 6;
            var vertices = new Vector3[sides + 2];

            for (var i = 0; i < sides; i++)
            {
                var angle = i*2f*(float) System.Math.PI/sides;
                vertices[i] = new Vector3((float) System.Math.Sin(angle), 0f, (float) System.Math.Cos(angle));
            }

            vertices[sides] = new Vector3(0f, 0.75f, 0f);
            vertices[sides + 1] = new Vector3(0f, -0.4f, 0f);

            var edges = new int[sides*3*2];
            var at = 0;
            for (var i = 0; i < sides; i++)
            {
                var next = (i + 1)%sides;
                edges[at++] = i;
                edges[at++] = next;
                edges[at++] = i;
                edges[at++] = sides;
                edges[at++] = i;
                edges[at++] = sides + 1;
            }

            return new WireModel(vertices, edges);
        }

        /// <summary>Builds a block, sitting on the ground rather than centred on it.</summary>
        /// <returns>The model.</returns>
        private static WireModel BuildCube()
        {
            const float top = 1.55f;
            var vertices = new[]
            {
                new Vector3(-1f, 0f, -1f), new Vector3(1f, 0f, -1f),
                new Vector3(1f, 0f, 1f), new Vector3(-1f, 0f, 1f),
                new Vector3(-1f, top, -1f), new Vector3(1f, top, -1f),
                new Vector3(1f, top, 1f), new Vector3(-1f, top, 1f)
            };

            var edges = new[] {0, 1, 1, 2, 2, 3, 3, 0, 4, 5, 5, 6, 6, 7, 7, 4, 0, 4, 1, 5, 2, 6, 3, 7};
            return new WireModel(vertices, edges);
        }

        /// <summary>Builds a pyramid.</summary>
        /// <returns>The model.</returns>
        private static WireModel BuildPyramid()
        {
            var vertices = new[]
            {
                new Vector3(-1f, 0f, -1f), new Vector3(1f, 0f, -1f),
                new Vector3(1f, 0f, 1f), new Vector3(-1f, 0f, 1f),
                new Vector3(0f, 2.1f, 0f)
            };

            var edges = new[] {0, 1, 1, 2, 2, 3, 3, 0, 0, 4, 1, 4, 2, 4, 3, 4};
            return new WireModel(vertices, edges);
        }

        /// <summary>Builds a shell: three strokes through a point.</summary>
        /// <returns>The model.</returns>
        private static WireModel BuildShell()
        {
            var vertices = new[]
            {
                new Vector3(-1f, 0f, 0f), new Vector3(1f, 0f, 0f),
                new Vector3(0f, -1f, 0f), new Vector3(0f, 1f, 0f),
                new Vector3(0f, 0f, -1f), new Vector3(0f, 0f, 1f)
            };

            return new WireModel(vertices, new[] {0, 1, 2, 3, 4, 5});
        }
    }
}
