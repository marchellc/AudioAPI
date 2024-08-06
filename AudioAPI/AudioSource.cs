using LabExtended.API.Npcs;
using LabExtended.API.Collections;

using System;

using UnityEngine;

using PlayerRoles;

using VoiceChat;

using System.IO;

namespace AudioAPI
{
    public class AudioSource : IDisposable
    {
        private NpcHandler m_Npc;
        private AudioPlayer m_Audio;

        private bool m_FullySpawned;

        public bool IsSpawned => m_Npc != null && m_Audio != null && m_FullySpawned;
        public bool IsPlaying => IsSpawned && m_Audio.IsPlaying;

        public AudioPlayer Player => m_Audio;

        public NpcHandler Npc => m_Npc;

        public Vector3 Position
        {
            get => m_Npc?.Player.Position ?? Vector3.zero;
            set => m_Npc!.Player.Position = value;
        }

        public VoiceChatChannel Channel
        {
            get => m_Audio?.Channel ?? VoiceChatChannel.None;
            set => m_Audio!.Channel = value;
        }

        public PlayerCollection ProximityReceivers { get; private set; } = new PlayerCollection();

        public event Action OnSpawned;
        public event Action OnDisposed;

        public void Play(Vector3 position, byte[] data)
            => Play(position, new MemoryStream(data));

        public void Play(Vector3 position, Stream audioStream)
        {
            if (!IsSpawned)
                throw new InvalidOperationException($"You must call the Spawn() method first.");

            m_Npc.Player.Position = position;
            m_Audio.Play(audioStream, true);
        }

        public void Spawn(Action callback = null)
        {
            if (IsSpawned)
                return;

            m_FullySpawned = false;

            NpcHandler.Spawn("Audio Source", RoleTypeId.Tutorial, null, null, null, npc =>
            {
                m_Npc = npc;
                m_Npc.Player.Scale = Vector3.zero;
                m_Npc.Player.Switches.IsVisibleInRemoteAdmin = true;

                m_Audio = new AudioPlayer();
                m_Audio.Initialize(true);

                m_Audio.OnUpdate += Update;
                m_Audio.Source = m_Npc.Player;

                m_FullySpawned = true;

                OnSpawned?.Invoke();

                callback?.Invoke();
            });
        }

        public void Dispose()
        {
            m_FullySpawned = false;

            m_Npc?.Destroy();
            m_Npc = null;

            ProximityReceivers?.Dispose();
            ProximityReceivers = null;

            if (m_Audio != null)
            {
                m_Audio.OnUpdate -= Update;
                m_Audio.Dispose();
                m_Audio = null;
            }

            OnDisposed?.Invoke();
            OnDisposed = null;

            OnSpawned = null;
        }

        private void Update()
        {
            if (!IsSpawned || !IsPlaying)
                return;

            ProximityReceivers.ForEach(x =>
            {
                if (!x.Role.IsAlive)
                    return;

                m_Npc.Player.FakePosition.SetValue(x, x.CameraTransform.position + x.CameraTransform.forward);
            });
        }
    }
}
