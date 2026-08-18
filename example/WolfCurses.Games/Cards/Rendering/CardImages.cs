// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 08/17/2026

using System;
using System.Collections.Generic;
using System.IO;
using WolfCurses.Graphics;

namespace WolfCurses.Games.Cards
{
    /// <summary>
    ///     Loads the fifty-two card faces and the back, once, and hands them out as pixels.
    ///     <para>
    ///         The artwork is from <c>github.com/saulspatz/SVGCards</c> (the <c>Vertical2</c> deck), placed in the
    ///         public domain by its author — the picture cards and jokers originate in Byron Knoll's public-domain
    ///         set and the backs from openclipart.org. No attribution is required; it is here because where art came
    ///         from is worth being able to look up. <b>Only the <c>pngs</c> folders of that repository are usable:
    ///         the SVGs are the source of truth there, and this library decodes PNG, JPEG and GIF and says so
    ///         plainly about anything else.</b> The same rule that made only <c>SmallPng/</c> usable for the chess
    ///         pieces.
    ///     </para>
    ///     <para>
    ///         Every card is 75x113 with an alpha channel, which matters twice: they can be laid over a table colour
    ///         of our choosing, and they can overlap into a fan without a rectangle of background cutting into the
    ///         card underneath.
    ///     </para>
    /// </summary>
    public sealed class CardImages
    {
        /// <summary>Where the artwork is copied to beside the executable.</summary>
        public const string DefaultFolder = "cards";

        /// <summary>Native width of one card in pixels.</summary>
        public const int CardWidth = 75;

        /// <summary>Native height of one card in pixels.</summary>
        public const int CardHeight = 113;

        private readonly Dictionary<Card, PixelBuffer> _faces = new();

        /// <summary>Initializes a new instance of the <see cref="CardImages" /> class, loading what it can find.</summary>
        /// <param name="folder">Where the artwork is; defaults to the folder copied beside the executable.</param>
        public CardImages(string folder = null)
        {
            Folder = folder ?? DefaultFolder;

            try
            {
                foreach (CardSuitEnum suit in Enum.GetValues(typeof (CardSuitEnum)))
                foreach (CardRankEnum rank in Enum.GetValues(typeof (CardRankEnum)))
                {
                    var card = new Card(rank, suit);
                    var pixels = Load(Path.Combine(Folder, card.ImageFile));
                    if (pixels != null)
                        _faces[card] = pixels;
                }

                Back = Load(Path.Combine(Folder, "back.png"));
            }
            catch (Exception exception)
            {
                // Never fatal. A missing folder means the games fall back to letters, which they can do perfectly
                // well - the same bargain AnsiImage strikes when a picture will not load.
                Error = exception.Message;
            }
        }

        /// <summary>Where the artwork was looked for.</summary>
        public string Folder { get; }

        /// <summary>The card back, or null when it could not be loaded.</summary>
        public PixelBuffer Back { get; }

        /// <summary>Why the artwork could not be loaded, when it could not.</summary>
        public string Error { get; private set; }

        /// <summary>Whether the whole deck plus its back is here, which is the only state worth drawing pictures in.</summary>
        public bool IsAvailable => _faces.Count == 52 && Back != null;

        /// <summary>The face of one card, or null when it is not loaded.</summary>
        /// <param name="card">Which card.</param>
        /// <returns>Its pixels.</returns>
        public PixelBuffer Face(Card card)
        {
            return _faces.TryGetValue(card, out var pixels) ? pixels : null;
        }

        /// <summary>Reads one PNG, or answers null rather than throwing.</summary>
        private PixelBuffer Load(string path)
        {
            if (!File.Exists(path))
            {
                Error ??= $"No card artwork at \"{Path.GetFullPath(path)}\".";
                return null;
            }

            using var stream = File.OpenRead(path);
            return ImageDecoders.Default.Decode(stream);
        }
    }
}
