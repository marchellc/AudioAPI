using LabExtended.API.Npcs;

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

        public void Play(Vector3 position, byte[] data)
            => Play(position, new MemoryStream(data));

        public void Play(Vector3 position, Stream audioStream)
        {
            m_Npc.Player.Position = position;
            m_Audio.Play(audioStream, true);
        }

        public void Stop()
            => m_Audio?.Stop(true);

        public void Spawn()
        {
            if (IsSpawned)
                return;

            m_FullySpawned = false;

            NpcHandler.Spawn("Audio Source", RoleTypeId.Tutorial, null, null, null, npc =>
            {
                m_Npc = npc;
                m_Npc.Player.Switches.IsVisibleInRemoteAdmin = true;

                m_Audio = new AudioPlayer();
                m_Audio.Initialize(true);
                m_Audio.Source = m_Npc.Player;

                m_FullySpawned = true;
            });
        }

        public void Dispose()
        {
            m_Npc?.Destroy();
            m_Npc = null;

            m_Audio?.Dispose();
            m_Audio = null;

            m_FullySpawned = false;
        }
    }
}
