using System;
using System.Diagnostics;
using System.Threading;
using WolfCurses.Apps.MediaPlayer;
using WolfCurses.Graphics;
using Xunit;

namespace WolfCurses.Apps.Tests
{
    /// <summary>
    ///     The media player's rules, with no console anywhere near them.
    ///     <para>
    ///         <b>Reading ffprobe's output is tested against text rather than against files</b>, which is what lets
    ///         the awkward cases be awkward: a fractional frame rate, an unknown length, a stream ffprobe says N/A
    ///         about, a cover picture pretending to be a film. Each is four lines here and would be four files
    ///         somebody had to find and licence otherwise.
    ///     </para>
    ///     <para>
    ///         <b>The tests that need ffmpeg say so and skip without it.</b> They are worth having, because the one
    ///         thing no amount of parsing proves is that a real decoder produces frames of the size it was asked
    ///         for - and getting that wrong tears every picture in half.
    ///     </para>
    /// </summary>
    public class MediaPlayerRulesTests
    {
        /// <summary>A file ffmpeg makes for us, so the pipeline tests need nothing shipped.</summary>
        private static readonly string _generated = "testsrc2=size=320x180:rate=25:duration=5";

        [Fact]
        public void ItReadsAFilmWithSoundInIt()
        {
            var info = MediaProbe.Parse(
                "[STREAM]\ncodec_name=h264\ncodec_type=video\nwidth=1280\nheight=720\nr_frame_rate=30/1\n[/STREAM]\n" +
                "[STREAM]\ncodec_name=aac\ncodec_type=audio\nchannels=2\nsample_rate=48000\n[/STREAM]\n" +
                "[FORMAT]\nduration=125.400000\n[/FORMAT]\n",
                "film.mp4");

            Assert.True(info.HasVideo);
            Assert.True(info.HasAudio);
            Assert.Equal(1280, info.Width);
            Assert.Equal(720, info.Height);
            Assert.Equal(30d, info.FrameRate);
            Assert.Equal(2, info.Channels);
            Assert.Equal(TimeSpan.FromSeconds(125.4d), info.Duration);
            Assert.Equal("h264 1280x720 30fps + aac 2ch", info.Summary());
        }

        [Fact]
        public void AFractionalFrameRateIsKeptAsAFraction()
        {
            // 24000/1001 is 23.976, and calling it 24 puts the picture a second ahead of the sound after about
            // forty minutes. This is the reason the fraction is parsed rather than rounded.
            var info = MediaProbe.Parse(
                "[STREAM]\ncodec_type=video\nwidth=1920\nheight=800\nr_frame_rate=24000/1001\n[/STREAM]\n",
                "film.mkv");

            Assert.Equal(24000d / 1001d, info.FrameRate, 6);
            Assert.NotEqual(24d, info.FrameRate);
        }

        [Fact]
        public void SomethingWithNoPictureInItIsSoundOnly()
        {
            var info = MediaProbe.Parse(
                "[STREAM]\ncodec_name=flac\ncodec_type=audio\nchannels=2\nsample_rate=44100\n[/STREAM]\n" +
                "[FORMAT]\nduration=213.000000\n[/FORMAT]\n",
                "song.flac");

            Assert.False(info.HasVideo);
            Assert.True(info.HasAudio);
            Assert.Equal("flac 2ch", info.Summary());
        }

        [Fact]
        public void ACoverPictureIsNotTreatedAsAFilm()
        {
            // An album cover is a video stream, and believing it means showing a still for two seconds and then
            // reporting that the song has finished. What gives it away is having no size ffprobe would state.
            var info = MediaProbe.Parse(
                "[STREAM]\ncodec_name=mjpeg\ncodec_type=video\nwidth=N/A\nheight=N/A\n[/STREAM]\n" +
                "[STREAM]\ncodec_name=mp3\ncodec_type=audio\nchannels=2\n[/STREAM]\n",
                "song.mp3");

            Assert.False(info.HasVideo);
            Assert.True(info.HasAudio);
        }

        [Fact]
        public void NotApplicableIsNotZero()
        {
            // Letting ffprobe's own "do not know" reach a number parse turns every unknown into a zero, and a zero
            // here reads as a measurement rather than as an absence.
            var info = MediaProbe.Parse(
                "[STREAM]\ncodec_type=audio\nchannels=N/A\n[/STREAM]\n[FORMAT]\nduration=N/A\n[/FORMAT]\n",
                "stream.ts");

            Assert.Equal(0, info.Channels);
            Assert.Equal(TimeSpan.Zero, info.Duration);
        }

        [Fact]
        public void TheFirstStreamOfEachKindWins()
        {
            // Which matches what the decoder picks when nothing tells it otherwise, so the file is described by
            // the track that will actually be heard.
            var info = MediaProbe.Parse(
                "[STREAM]\ncodec_name=aac\ncodec_type=audio\nchannels=2\n[/STREAM]\n" +
                "[STREAM]\ncodec_name=ac3\ncodec_type=audio\nchannels=6\n[/STREAM]\n",
                "film.mkv");

            Assert.Equal("aac", info.AudioCodec);
            Assert.Equal(2, info.Channels);
        }

        [Fact]
        public void AFileNothingCouldBeLearnedAboutIsStillWorthTrying()
        {
            // ffprobe missing is not a reason to refuse to play something, so the extension is used as the guess
            // and being wrong costs a message rather than anything worse.
            var video = MediaProbe.Parse(string.Empty, "holiday.mkv");
            var audio = MediaProbe.Parse(string.Empty, "song.mp3");

            Assert.True(video.HasVideo);
            Assert.True(video.HasAudio);
            Assert.False(audio.HasVideo);
            Assert.True(audio.HasAudio);
        }

        [Fact]
        public void AnUnclosedSectionIsStillRead()
        {
            // What a killed ffprobe leaves behind, and losing the last stream because of a missing bracket would
            // be a file that plays with no sound for no reason anybody could see.
            var info = MediaProbe.Parse("[STREAM]\ncodec_name=vp9\ncodec_type=video\nwidth=640\nheight=480\n", "a.webm");

            Assert.True(info.HasVideo);
            Assert.Equal(640, info.Width);
        }

        [Fact]
        public void ASummaryLeavesOutWhatItDoesNotKnow()
        {
            var bare = new MediaInfo {HasVideo = true};

            Assert.Equal("video", bare.Summary());
            Assert.Equal("nothing playable in it", new MediaInfo().Summary());
        }

        [Fact]
        public void SomethingGeneratedIsCalledWhatItWasNamedRatherThanItsFilterGraph()
        {
            var generated = new MediaInfo {Path = "sine=frequency=440", Title = "Test tone"};
            var file = new MediaInfo {Path = @"C:\music\song.mp3"};

            Assert.Equal("Test tone", generated.Name);
            Assert.Equal("song.mp3", file.Name);
            Assert.Equal("Nothing open", new MediaInfo().Name);
        }

        [Fact]
        public void APictureIsAskedForAtExactlyTheSizeTheRendererWants()
        {
            // The whole performance story in one line: produce the pixels at the size the renderer puts in a cell
            // and nothing is ever resampled on this side.
            var blocks = StageView.PixelSize(new HalfBlockImageRenderer(), 78, 16);
            var sixel = StageView.PixelSize(new SixelImageRenderer(), 78, 16);

            Assert.Equal((78, 32), blocks);
            Assert.Equal((780, 320), sixel);
        }

        [Fact]
        public void APictureSizeIsAlwaysEven()
        {
            // Every codec worth using wants even numbers and some refuse an odd one outright.
            var size = StageView.PixelSize(new HalfBlockImageRenderer(), 77, 15);

            Assert.Equal(0, size.Width % 2);
            Assert.Equal(0, size.Height % 2);
        }

        [Fact]
        public void ASilentBlockLeavesEveryBarAtNothing()
        {
            var bands = new double[16];

            Spectrum.Compute(new short[AudioPipe.BlockSamples], bands);

            Assert.All(bands, band => Assert.Equal(0d, band));
        }

        [Fact]
        public void ABlockOfTheWrongSizeIsIgnoredRatherThanRead()
        {
            var bands = new double[8];
            Array.Fill(bands, 0.5d);

            Spectrum.Compute(new short[7], bands);
            Assert.All(bands, band => Assert.Equal(0d, band));

            Array.Fill(bands, 0.5d);
            Spectrum.Compute(null, bands);
            Assert.All(bands, band => Assert.Equal(0d, band));
        }

        [Fact]
        public void ATonePutsItsEnergyInTheBandThatCoversIt()
        {
            // The test that says the transform is a transform rather than a plausible-looking noise generator: a
            // pure tone has to light one region and leave the rest alone, and it has to move when the tone does.
            var bands = new double[24];

            Spectrum.Compute(Tone(440d), bands);
            var low = Loudest(bands);

            Spectrum.Compute(Tone(3000d), bands);
            var high = Loudest(bands);

            Assert.True(high > low, "3kHz landed at band " + high + " and 440Hz at band " + low);

            // And the bands are spaced by ear rather than evenly, so an octave is the same distance wherever it
            // is. Two octaves up from 440 must be about as far along as two octaves up from 220.
            Spectrum.Compute(Tone(220d), bands);
            var below = Loudest(bands);

            Spectrum.Compute(Tone(880d), bands);
            var above = Loudest(bands);

            Assert.True(Math.Abs(low - below - (above - low)) <= 1,
                "octaves are not evenly spaced: " + below + ", " + low + ", " + above);
        }

        [Fact]
        public void TheDecoderProducesFramesOfExactlyTheSizeItWasAskedFor()
        {
            Assert.SkipUnless(FfmpegTools.HasFfmpeg, "ffmpeg is not on this machine.");

            // Nothing here proves the arithmetic like a real decoder does: a frame one row short is read as part
            // of the next one, and every picture from then on is torn.
            using var pipe = new VideoPipe(_generated, TimeSpan.Zero, 160, 90, 25d, true);

            var clock = Stopwatch.StartNew();
            var frames = 0;

            while (frames < 5 && clock.Elapsed < TimeSpan.FromSeconds(30d))
            {
                if (pipe.TryRead(out var frame))
                {
                    Assert.Equal(160, frame.Width);
                    Assert.Equal(90, frame.Height);
                    Assert.Equal(160 * 90 * 4, frame.Data.Length);

                    pipe.Recycle(frame);
                    frames++;
                    continue;
                }

                if (pipe.IsFinished)
                    break;

                Thread.Sleep(5);
            }

            Assert.True(frames >= 5, "only " + frames + " frames arrived: " + pipe.Error);
        }

        [Fact]
        public void ADecodedFrameFillsTheStageExactly()
        {
            Assert.SkipUnless(FfmpegTools.HasFfmpeg, "ffmpeg is not on this machine.");

            var size = StageView.PixelSize(ImageRenderers.Default, 78, 16);

            using var pipe = new VideoPipe(_generated, TimeSpan.Zero, size.Width, size.Height, 25d, true);

            var clock = Stopwatch.StartNew();
            PixelBuffer frame = null;

            while (frame == null && clock.Elapsed < TimeSpan.FromSeconds(30d))
            {
                if (!pipe.TryRead(out frame))
                    Thread.Sleep(5);
            }

            Assert.NotNull(frame);

            var rows = StageView.Picture(frame, 78, 16);

            Assert.Equal(16, rows.Count);
            Assert.All(rows, row => Assert.True(AnsiText.VisibleLength(row) <= 78));

            // And it is a picture rather than sixteen blank rows, which is what a wrong pixel format looks like.
            Assert.Contains(rows, row => AnsiText.StripEscapes(row).Trim().Length > 0);
        }

        [Fact]
        public void TheSoundPipeHandsOutBlocksAgainstTheClock()
        {
            Assert.SkipUnless(FfmpegTools.HasFfmpeg, "ffmpeg is not on this machine.");

            using var pipe = new AudioPipe("sine=frequency=440:duration=5", TimeSpan.Zero, true);

            var clock = Stopwatch.StartNew();
            var blocks = 0;

            while (blocks < 10 && clock.Elapsed < TimeSpan.FromSeconds(20d))
            {
                if (pipe.TryReadAt(clock.Elapsed, out var block))
                {
                    Assert.Equal(AudioPipe.BlockSamples, block.Length);
                    pipe.Recycle(block);
                    blocks++;
                    continue;
                }

                Thread.Sleep(5);
            }

            Assert.True(blocks >= 10, "only " + blocks + " blocks arrived");
        }

        [Fact]
        public void FramesAreCountedFromWhereThePipeStartedAndNotFromTheFilm()
        {
            var from = TimeSpan.FromSeconds(20d);

            // The bug this pins: comparing frames taken against the clock's own frame number makes a player that
            // has just seeked twenty seconds in believe it is six hundred frames behind, so it drains the pipe as
            // fast as ffmpeg can fill it and locks up for a moment after every seek.
            Assert.Equal(1L, VideoPipe.FramesDue(from, from, 30d));
            Assert.Equal(1L, VideoPipe.FramesDue(TimeSpan.FromSeconds(20.03d), from, 30d));
            Assert.Equal(2L, VideoPipe.FramesDue(TimeSpan.FromSeconds(20.04d), from, 30d));
            Assert.Equal(31L, VideoPipe.FramesDue(TimeSpan.FromSeconds(21d), from, 30d));
        }

        [Fact]
        public void APipeStartedAtTheBeginningCountsFromTheBeginning()
        {
            Assert.Equal(1L, VideoPipe.FramesDue(TimeSpan.Zero, TimeSpan.Zero, 30d));
            Assert.Equal(301L, VideoPipe.FramesDue(TimeSpan.FromSeconds(10d), TimeSpan.Zero, 30d));
        }

        [Fact]
        public void AClockBehindThePipeAsksForTheFirstFrameRatherThanANegativeOne()
        {
            // Which happens for a moment after a seek, since the clock is moved before the new pipe is opened.
            Assert.Equal(1L, VideoPipe.FramesDue(TimeSpan.FromSeconds(5d), TimeSpan.FromSeconds(20d), 30d));
            Assert.Equal(1L, VideoPipe.FramesDue(TimeSpan.FromSeconds(30d), TimeSpan.FromSeconds(20d), 0d));
        }

        [Fact]
        public void AskingForFewerPixelsKeepsThePictureTheSameSizeOnScreen()
        {
            var renderer = new SixelImageRenderer();

            // The whole trick: the columns and rows never change, only how many pixels are put in them. A
            // true-pixel renderer stretches what it is handed rather than resampling it, so this is nearly free
            // and it is what makes a 4K film play at thirty a second instead of eleven.
            var full = StageView.PixelSize(renderer, 78, 16);
            var half = StageView.PixelSize(renderer, 78, 16, 2);
            var third = StageView.PixelSize(renderer, 78, 16, 3);

            Assert.Equal((780, 320), full);
            Assert.Equal((390, 160), half);
            Assert.Equal((260, 106), third);
        }

        [Fact]
        public void EveryQualityStillGivesAnEvenSizeThatACodecWillAccept()
        {
            var renderer = new SixelImageRenderer();

            for (var quality = 1; quality <= 8; quality++)
            {
                var size = StageView.PixelSize(renderer, 77, 15, quality);

                Assert.Equal(0, size.Width % 2);
                Assert.Equal(0, size.Height % 2);
                Assert.True(size.Width >= 2 && size.Height >= 2, "quality " + quality + " gave " + size);
            }
        }

        [Fact]
        public void StoppingTheSoundReallyEndsTheProgramPlayingIt()
        {
            Assert.SkipUnless(AudioPlayer.IsAvailable, "ffplay is not on this machine.");

            // The screen test that thought it covered this only ever looked at the screen, and Stop swallows the
            // exceptions a failed kill would throw - so it would have passed against a player that stopped
            // nothing. Counting the actual programs is the only assertion worth anything here.
            var before = Running();

            var player = new AudioPlayer {IsMuted = true};
            player.PlayFrom("sine=frequency=440:duration=600", TimeSpan.Zero, true);

            Assert.True(WaitUntil(() => Running() > before), "ffplay never started");

            player.Stop();

            Assert.True(WaitUntil(() => Running() <= before), "ffplay was still running after Stop");
        }

        [Fact]
        public void AChildIsPutInAJobSoItCannotOutliveThisProcessHoweverThisProcessDies()
        {
            Assert.SkipUnless(AudioPlayer.IsAvailable, "ffplay is not on this machine.");
            Assert.SkipUnless(OperatingSystem.IsWindows(), "job objects are a Windows thing.");

            // Skipped when this process is already in somebody else's job - some build agents do that - because
            // then the answer below would be yes whatever this code did, and a test that cannot fail is worse
            // than no test.
            Assert.SkipWhen(InAnyJob(Process.GetCurrentProcess()), "this process is already in a job.");

            var player = new AudioPlayer {IsMuted = true};
            player.PlayFrom("sine=frequency=440:duration=600", TimeSpan.Zero, true);

            try
            {
                Assert.True(WaitUntil(() => Process.GetProcessesByName("ffplay").Length > 0), "ffplay never started");

                // The layer that saves you when nothing of ours gets to run: the X button on a console window, a
                // debugger being stopped, a kill from the task manager. The job is marked kill-on-close, so the
                // operating system ends the child when this process dies for any reason at all.
                foreach (var child in Process.GetProcessesByName("ffplay"))
                    Assert.True(InAnyJob(child), "a child was started outside the job");
            }
            finally
            {
                player.Stop();
            }
        }

        /// <summary>How many copies of ffplay are running just now.</summary>
        private static int Running()
        {
            return Process.GetProcessesByName("ffplay").Length;
        }

        /// <summary>Waits a few seconds for something to become true, since starting a program is not instant.</summary>
        private static bool WaitUntil(Func<bool> settled)
        {
            var clock = Stopwatch.StartNew();

            while (clock.Elapsed < TimeSpan.FromSeconds(10d))
            {
                if (settled())
                    return true;

                Thread.Sleep(50);
            }

            return settled();
        }

        /// <summary>Whether a process belongs to any job object at all.</summary>
        private static bool InAnyJob(Process process)
        {
            try
            {
                return IsProcessInJob(process.Handle, IntPtr.Zero, out var answer) && answer;
            }
            catch (Exception exception) when (exception is InvalidOperationException
                                                  or System.ComponentModel.Win32Exception
                                                  or NotSupportedException)
            {
                return false;
            }
        }

        [System.Runtime.InteropServices.DllImport("kernel32.dll", SetLastError = true)]
        [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
        private static extern bool IsProcessInJob(IntPtr process, IntPtr job,
            [System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
            out bool answer);

        [Fact]
        public void WhatIsInstalledIsReportedAsThreeSeparateAnswers()
        {
            var report = FfmpegTools.Report();

            // Three lines whatever the answer, because the three failures are different and rolling them into one
            // would report two of them wrongly.
            Assert.Equal(3, report.Count);
            Assert.Contains("ffmpeg", report[0], StringComparison.Ordinal);
            Assert.Contains("ffprobe", report[1], StringComparison.Ordinal);
            Assert.Contains("ffplay", report[2], StringComparison.Ordinal);
        }

        [Fact]
        public void WhatCountsAsAPictureFileIsDecidedByItsName()
        {
            Assert.True(MediaLibrary.LooksLikeVideo("holiday.MKV"));
            Assert.True(MediaLibrary.LooksLikeVideo(@"C:\films\a.mp4"));
            Assert.False(MediaLibrary.LooksLikeVideo("song.flac"));
            Assert.False(MediaLibrary.LooksLikeVideo(null));
        }

        /// <summary>The loudest band of a set.</summary>
        /// <param name="bands">The bands.</param>
        /// <returns>Its index.</returns>
        private static int Loudest(double[] bands)
        {
            var at = 0;

            for (var i = 1; i < bands.Length; i++)
            {
                if (bands[i] > bands[at])
                    at = i;
            }

            return at;
        }

        /// <summary>A block of samples holding one pure tone.</summary>
        /// <param name="hertz">The frequency.</param>
        /// <returns>The block.</returns>
        private static short[] Tone(double hertz)
        {
            var block = new short[AudioPipe.BlockSamples];

            for (var i = 0; i < block.Length; i++)
                block[i] = (short) (Math.Sin(2d * Math.PI * hertz * i / AudioPipe.SampleRate) * 20000d);

            return block;
        }
    }
}
