﻿// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using ManagedBass;
using osu.Framework.Audio.Track;

namespace osu.Framework.Audio.Sample
{
    public sealed class SampleChannelBass : SampleChannel, IBassAudio
    {
        private volatile int channel;
        private volatile bool playing;

        public override bool IsLoaded => Sample.IsLoaded;

        private BassRelativeFrequencyHandler relativeFrequencyHandler;
        private BassAmplitudeProcessor bassAmplitudeProcessor;

        public SampleChannelBass(Sample sample, Action<SampleChannel> onPlay)
            : base(sample, onPlay)
        {
        }

        void IBassAudio.UpdateDevice(int deviceIndex)
        {
            // Channels created from samples can not be migrated, so we need to ensure
            // a new channel is created after switching the device. We do not need to
            // manually free the channel, because our Bass.Free call upon switching devices
            // takes care of that.
            channel = 0;
        }

        internal override void OnStateChanged()
        {
            base.OnStateChanged();

            if (channel == 0)
                return;

            Bass.ChannelSetAttribute(channel, ChannelAttribute.Volume, AggregateVolume.Value);
            Bass.ChannelSetAttribute(channel, ChannelAttribute.Pan, AggregateBalance.Value);
            relativeFrequencyHandler.UpdateChannelFrequency(AggregateFrequency.Value);
        }

        public override bool Looping
        {
            get => base.Looping;
            set
            {
                base.Looping = value;
                setLoopFlag(Looping);
            }
        }

        public override void Play(bool restart = true)
        {
            base.Play(restart);

            EnqueueAction(() =>
            {
                if (!IsLoaded)
                {
                    channel = 0;
                    return;
                }

                // Free previous channels as we're creating a new channel for every playback, since old channels
                // may be overriden when too many other channels are created from the same sample.
                if (Bass.ChannelIsActive(channel) != PlaybackState.Stopped)
                    Bass.ChannelStop(channel);

                channel = ((SampleBass)Sample).CreateChannel();

                Bass.ChannelSetAttribute(channel, ChannelAttribute.NoRamp, 1);
                setLoopFlag(Looping);

                if (relativeFrequencyHandler == null)
                {
                    relativeFrequencyHandler = new BassRelativeFrequencyHandler(channel)
                    {
                        RequestZeroFrequencyPause = () => Bass.ChannelPause(channel),
                        RequestZeroFrequencyResume = () => Bass.ChannelPlay(channel),
                    };
                }
                else
                    relativeFrequencyHandler.SetChannel(channel);

                bassAmplitudeProcessor?.SetChannel(channel);
            });

            InvalidateState();

            EnqueueAction(() =>
            {
                if (channel != 0 && !relativeFrequencyHandler.ZeroFrequencyPauseRequested)
                    Bass.ChannelPlay(channel, restart);
            });

            // Needs to happen on the main thread such that
            // Played does not become true for a short moment.
            playing = true;
        }

        protected override void UpdateState()
        {
            playing = channel != 0 && Bass.ChannelIsActive(channel) != 0;
            base.UpdateState();

            bassAmplitudeProcessor?.Update();
        }

        public override void Stop()
        {
            base.Stop();

            EnqueueAction(() =>
            {
                if (channel == 0) return;

                Bass.ChannelStop(channel);

                // ChannelStop frees the channel.
                channel = 0;
            });
        }

        public override bool Playing => playing;

        public override ChannelAmplitudes CurrentAmplitudes => (bassAmplitudeProcessor ??= new BassAmplitudeProcessor(channel)).CurrentAmplitudes;

        private void setLoopFlag(bool value) => EnqueueAction(() =>
        {
            if (channel != 0)
                Bass.ChannelFlags(channel, value ? BassFlags.Loop : BassFlags.Default, BassFlags.Loop);
        });
    }
}
