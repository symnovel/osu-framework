﻿// Copyright (c) 2007-2017 ppy Pty Ltd <contact@ppy.sh>.
// Licensed under the MIT Licence - https://raw.githubusercontent.com/ppy/osu-framework/master/LICENCE

using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Logging;
using OpenTK;
using OpenTK.Graphics;
using osu.Framework.Allocation;
using osu.Framework.Configuration;

namespace osu.Framework.Graphics.Visualisation
{
    internal class LogOverlay : OverlayContainer
    {
        private readonly FillFlowContainer flow;

        protected override bool HideOnEscape => false;

        private Bindable<bool> enabled;

        private readonly Box box;

        private const float background_alpha = 0.6f;

        public override bool HandleInput => false;

        public LogOverlay()
        {
            //todo: use Input as font

            Width = 700;
            AutoSizeAxes = Axes.Y;

            Anchor = Anchor.BottomLeft;
            Origin = Anchor.BottomLeft;

            Margin = new MarginPadding(1);

            Masking = true;

            Children = new Drawable[]
            {
                box = new Box {
                    RelativeSizeAxes = Axes.Both,
                    Colour = Color4.Black,
                    Alpha = background_alpha,
                },
                flow = new FillFlowContainer
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                }
            };
        }

        protected override void LoadComplete()
        {

            base.LoadComplete();

            addEntry(new LogEntry
            {
                Level = LogLevel.Important,
                Message = "The debug log overlay is currently being displayed. You can toggle with Ctrl+F10 at any point.",
                Target = LoggingTarget.Information,
            });
        }

        private void addEntry(LogEntry entry)
        {
#if !DEBUG
            if (entry.Level <= LogLevel.Verbose)
                return;
#endif

            Schedule(() =>
            {
                const int display_length = 4000;

                var drawEntry = new DrawableLogEntry(entry);

                flow.Add(drawEntry);

                drawEntry.FadeInFromZero(800, EasingTypes.OutQuint);
                using (drawEntry.BeginDelayedSequence(display_length))
                    drawEntry.FadeOut(800, EasingTypes.InQuint);
                drawEntry.Expire();
            });
        }

        [BackgroundDependencyLoader]
        private void load(FrameworkConfigManager config)
        {
            enabled = config.GetBindable<bool>(FrameworkSetting.ShowLogOverlay);
            enabled.ValueChanged += val => State = val ? Visibility.Visible : Visibility.Hidden;
            enabled.TriggerChange();
        }

        protected override void PopIn()
        {
            Logger.NewEntry += addEntry;
            enabled.Value = true;
            FadeIn(100);
        }

        protected override void PopOut()
        {
            Logger.NewEntry -= addEntry;
            enabled.Value = false;
            FadeOut(100);
        }
    }

    internal class DrawableLogEntry : Container
    {
        private const float target_box_width = 65;

        private const float font_size = 14;

        public DrawableLogEntry(LogEntry entry)
        {
            RelativeSizeAxes = Axes.X;
            AutoSizeAxes = Axes.Y;

            Color4 col = getColourForEntry(entry);

            Children = new Drawable[]
            {
                new Container
                {
                    //log target coloured box
                    Margin = new MarginPadding(3),
                    Size = new Vector2(target_box_width, font_size),
                    Anchor = Anchor.CentreLeft,
                    Origin = Anchor.CentreLeft,
                    CornerRadius = 5,
                    Masking = true,
                    Children = new Drawable[]
                    {
                        new Box
                        {
                            RelativeSizeAxes = Axes.Both,
                            Colour = col,
                        },
                        new SpriteText
                        {
                            Anchor = Anchor.Centre,
                            Origin = Anchor.Centre,
                            Shadow = true,
                            ShadowColour = Color4.Black,
                            Margin = new MarginPadding { Left = 5, Right = 5 },
                            TextSize = font_size,
                            Text = entry.Target.ToString(),
                        }
                    }
                },
                new Container
                {
                    AutoSizeAxes = Axes.Y,
                    RelativeSizeAxes = Axes.X,
                    Anchor = Anchor.CentreLeft,
                    Origin = Anchor.CentreLeft,
                    Padding = new MarginPadding { Left = target_box_width + 10 },
                    Children = new Drawable[]
                    {
                        new SpriteText
                        {
                            AutoSizeAxes = Axes.Y,
                            RelativeSizeAxes = Axes.X,
                            TextSize = font_size,
                            Text = entry.Message
                        }
                    }
                }
            };
        }

        private Color4 getColourForEntry(LogEntry entry)
        {
            switch (entry.Target)
            {
                case LoggingTarget.Runtime:
                    return Color4.YellowGreen;
                case LoggingTarget.Network:
                    return Color4.BlueViolet;
                case LoggingTarget.Tournament:
                    return Color4.Yellow;
                case LoggingTarget.Performance:
                    return Color4.HotPink;
                case LoggingTarget.Debug:
                    return Color4.DarkBlue;
                case LoggingTarget.Information:
                    return Color4.CadetBlue;
                default:
                    return Color4.Cyan;
            }
        }
    }
}
