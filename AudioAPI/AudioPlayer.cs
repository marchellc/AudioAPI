using LabExtended.API;
using LabExtended.API.Collections;

using LabExtended.Core;
using LabExtended.Core.Ticking;

using MEC;

using NorthwoodLib.Pools;

using NVorbis;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;

using UnityEngine;

using VoiceChat;
using VoiceChat.Codec;
using VoiceChat.Codec.Enums;

using VoiceChat.Networking;

namespace AudioAPI
{
    public class AudioPlayer
    {
        private static int _idClock = 0;

        public static int HeadSamples => Loader.Instance.Config.HeadSamples;
        public static OpusApplicationType OpusType => Loader.Instance.Config.OpusType;

        private bool m_StopTrack = false;
        private bool m_SelfUpdate = false;
        private bool m_SendReady = false;

        private int m_SamplesPerSec = 0;
        private int m_ReadCount = 0;

        private float m_AllowedSamples = 0f;

        private float[] m_SendBuffer;
        private float[] m_ReadBuffer;

        private byte[] m_EncodedBuffer;

        private CoroutineHandle m_Playback;

        private VorbisReader m_Reader;
        private OpusEncoder m_Encoder;
        private Queue<float> m_StreamBuffer;
        private PlaybackBuffer m_PlaybackBuffer;

        public bool IsPlaying { get; private set; } = false;
        public bool IsReady { get; private set; } = false;

        public bool IsPaused { get; set; } = false;
        public bool IsLooping { get; set; } = false;

        public virtual bool IsSourceReady => Source != null;

        public float Volume { get; set; } = 100f;

        public int Id { get; } = _idClock++;

        public virtual ExPlayer Source { get; set; } = null;

        public Stream Current { get; private set; } = null;

        public ConcurrentQueue<Stream> Queue { get; } = new ConcurrentQueue<Stream>();

        public PlayerCollection Receivers { get; } = new PlayerCollection();

        public VoiceChatChannel Channel { get; set; } = VoiceChatChannel.Proximity;

        public event Action OnFinished;
        public event Action OnStarted;
        public event Action OnInitialized;
        public event Action OnDisposed;
        public event Action OnUpdate;

        public virtual void Initialize(bool selfUpdate = false)
        {
            Dispose();

            m_Encoder = new OpusEncoder(OpusType);
            m_PlaybackBuffer = new PlaybackBuffer();

            m_StreamBuffer = new Queue<float>();

            m_EncodedBuffer = new byte[512];

            m_StopTrack = false;
            m_SendReady = false;

            m_AllowedSamples = 0f;
            m_SamplesPerSec = 0;
            m_ReadCount = 0;

            m_SelfUpdate = selfUpdate;

            if (m_SelfUpdate)
                TickManager.SubscribeTick(Update, TickTimer.NoneProfiled, null, true);

            IsReady = true;

            OnInitialized?.Invoke();
        }

        public virtual void Play(Stream stream, bool playImmediate = false)
        {
            if (stream is null)
                throw new ArgumentNullException(nameof(stream));

            if (playImmediate || !IsPlaying)
            {
                InternalPlay(stream);
                return;
            }

            Queue.Enqueue(stream);
        }

        public void Pause()
            => IsPaused = true;

        public void Resume()
            => IsPaused = false;

        public void Shuffle()
        {
            var list = ListPool<Stream>.Shared.Rent(Queue);

            list.ShuffleList();

            foreach (var stream in list)
                Queue.Enqueue(stream);

            ListPool<Stream>.Shared.Return(list);
        }

        public void Stop(bool clearQueue = false)
        {
            m_StopTrack = true;

            if (clearQueue)
                Clear();
        }

        public virtual void Send(ExPlayer receiver, ref VoiceMessage message)
        {
            try
            {
                receiver.Connection.Send(message);
            }
            catch (Exception ex)
            {
                Error(ex);
            }
        }

        public virtual void Clear()
        {
            while (Queue.TryDequeue(out var stream))
                stream.Dispose();
        }

        public virtual void Prepare() { }

        public virtual void Dispose()
        {
            try
            {
                if (TickManager.IsRunning(Update))
                    TickManager.UnsubscribeTick(Update);

                Timing.KillCoroutines(m_Playback);

                if (m_Encoder != null)
                {
                    m_Encoder.Dispose();
                    m_Encoder = null;
                }

                if (m_PlaybackBuffer != null)
                {
                    m_PlaybackBuffer.Dispose();
                    m_PlaybackBuffer = null;
                }

                if (m_StreamBuffer != null)
                {
                    m_StreamBuffer.Clear();
                    m_StreamBuffer = null;
                }

                if (m_Reader != null)
                {
                    m_Reader.Dispose();
                    m_Reader = null;
                }

                if (Current != null)
                {
                    Current.Dispose();
                    Current = null;
                }

                m_StopTrack = false;
                m_SelfUpdate = false;
                m_SendReady = false;

                m_SamplesPerSec = 0;
                m_AllowedSamples = 0f;
                m_ReadCount = 0;

                m_SendBuffer = null;
                m_ReadBuffer = null;
                m_EncodedBuffer = null;

                IsReady = false;
                IsPaused = false;
                IsLooping = false;
                IsPlaying = false;

                Volume = 100f;

                Clear();

                Receivers.Dispose();

                OnDisposed?.Invoke();
                OnDisposed = null;

                OnStarted = null;
                OnFinished = null;
                OnInitialized = null;
                OnUpdate = null;
            }
            catch (Exception ex)
            {
                Error(ex);
            }
        }

        public virtual void Update()
        {
            try
            {
                if (!m_SendReady || m_StreamBuffer is null || m_EncodedBuffer is null || m_StreamBuffer.Count < 1 || !IsSourceReady)
                    return;

                OnUpdate?.Invoke();

                m_AllowedSamples += Time.deltaTime * m_SamplesPerSec;

                var copyCount = Mathf.Min(Mathf.FloorToInt(m_AllowedSamples), m_StreamBuffer.Count);

                for (int i = 0; i < copyCount; i++)
                    m_PlaybackBuffer.Write(m_StreamBuffer.Dequeue() * (Volume / 100f));

                m_AllowedSamples -= copyCount;

                while (m_PlaybackBuffer.Length >= VoiceChatSettings.PacketSizePerChannel)
                {
                    m_PlaybackBuffer.ReadTo(m_SendBuffer, VoiceChatSettings.PacketSizePerChannel);

                    var length = m_Encoder.Encode(m_SendBuffer, m_EncodedBuffer, VoiceChatSettings.PacketSizePerChannel);
                    var message = new VoiceMessage(Source.Hub, Channel, m_EncodedBuffer, length, false);

                    Receivers.ForEach(x => Send(x, ref message));
                }
            }
            catch (Exception ex)
            {
                Error(ex);
            }
        }

        private void InternalPlay(Stream stream)
        {
            try
            {
                InternalDisposePlayback(false, false);

                m_Playback = Timing.RunCoroutine(InternalPlayback(stream));
            }
            catch (Exception ex)
            {
                Error(ex);
            }
        }

        private void InternalDisposePlayback(bool playNext = false, bool ignoreLoop = false)
        {
            try
            {
                IsPlaying = false;

                m_StopTrack = false;
                m_SendReady = false;

                m_SamplesPerSec = 0;
                m_AllowedSamples = 0f;
                m_ReadCount = 0;

                m_SendBuffer = null;
                m_ReadBuffer = null;

                m_StreamBuffer.Clear();

                if (m_Reader != null)
                {
                    m_Reader.Dispose();
                    m_Reader = null;
                }

                if (playNext)
                {
                    OnFinished?.Invoke();

                    if (IsLooping && !ignoreLoop)
                    {
                        InternalPlay(Current);
                        return;
                    }
                    else
                    {
                        if (Queue.TryDequeue(out var next))
                        {
                            Current?.Dispose();
                            Current = next;

                            InternalPlay(next);
                        }
                        else
                        {
                            Current?.Dispose();
                            Current = null;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Error(ex);
            }
        }

        private IEnumerator<float> InternalPlayback(Stream stream)
        {
            Prepare();

            while (!IsSourceReady)
                yield return Timing.WaitForOneFrame;

            try
            {
                if (stream.CanSeek)
                    stream.Seek(0, SeekOrigin.Begin);
            }
            catch (Exception ex)
            {
                Error(ex);
            }

            Current = stream;

            m_Reader = new VorbisReader(stream);

            if (m_Reader.Channels != VoiceChatSettings.Channels)
            {
                Warn($"Attempted to play audio with more than &1{VoiceChatSettings.Channels}&r channel.");

                InternalDisposePlayback(true, true);
                yield break;
            }

            if (m_Reader.SampleRate != VoiceChatSettings.SampleRate)
            {
                Warn($"Attempted to play audio with a sample rate of &3{m_Reader.SampleRate} Hz&r, &1{VoiceChatSettings.SampleRate} Hz&r is required.");

                InternalDisposePlayback(true, true);
                yield break;
            }

            m_SamplesPerSec = VoiceChatSettings.SampleRate * VoiceChatSettings.Channels;

            m_SendBuffer = new float[m_SamplesPerSec / 5 + HeadSamples];
            m_ReadBuffer = new float[m_SamplesPerSec / 5 + HeadSamples];

            IsPlaying = true;

            OnStarted?.Invoke();

            while ((m_ReadCount = m_Reader.ReadSamples(m_ReadBuffer, 0, m_ReadBuffer.Length)) > 0)
            {
                if (m_StopTrack)
                {
                    m_Reader.SeekTo(m_Reader.TotalSamples - 1);
                    m_StopTrack = false;
                }

                while (IsPaused)
                    yield return Timing.WaitForOneFrame;

                if (m_StreamBuffer.Count >= m_ReadBuffer.Length)
                {
                    m_SendReady = true;
                    yield return Timing.WaitForOneFrame;
                }

                for (int i = 0; i < m_ReadBuffer.Length; i++)
                    m_StreamBuffer.Enqueue(m_ReadBuffer[i]);
            }

            while (m_StreamBuffer.Count > 0 && !m_StopTrack)
            {
                m_SendReady = true;
                yield return Timing.WaitForOneFrame;
            }

            InternalDisposePlayback(true, false);
        }

        public static void Info(object msg)
            => ExLoader.Info("Audio Player", msg);

        public static void Warn(object msg)
            => ExLoader.Warn("Audio Player", msg);

        public static void Error(object msg)
            => ExLoader.Error("Audio Player", msg);
    }
}