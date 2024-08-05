using LabExtended.API;

using System;
using System.IO;
using System.Linq;

namespace AudioAPI.Extensions
{
    public static class PlayerExtensions
    {
        public static void Play(this AudioPlayer player, byte[] data, bool playImmediate = false)
        {
            if (player is null)
                throw new ArgumentNullException(nameof(player));

            if (data is null)
                throw new ArgumentNullException(nameof(data));

            player.Play(new MemoryStream(data), playImmediate);
        }

        public static void RemoveReceiver(this AudioPlayer player, ExPlayer ply)
        {
            if (player is null)
                throw new ArgumentNullException(nameof(player));

            if (ply is null)
                throw new ArgumentNullException(nameof(ply));

            player.Receivers.RemoveWhere(x => x.PlayerId == ply.PlayerId);
        }

        public static void RemoveReceivers(this AudioPlayer player, Predicate<ExPlayer> predicate)
        {
            if (player is null)
                throw new ArgumentNullException(nameof(player));

            if (predicate is null)
                throw new ArgumentNullException(nameof(predicate));

            player.Receivers.RemoveWhere(x => predicate(x));
        }

        public static bool AddReceiver(this AudioPlayer player, ExPlayer receiver)
        {
            if (player is null)
                throw new ArgumentNullException(nameof(player));

            if (receiver is null)
                throw new ArgumentNullException(nameof(receiver));

            if (player.Receivers.Any(x => x.PlayerId == receiver.PlayerId))
                return false;

            return player.Receivers.Add(receiver);
        }

        public static int AddReceivers(this AudioPlayer player, Predicate<ExPlayer> predicate)
        {
            if (player is null)
                throw new ArgumentNullException(nameof(player));

            if (predicate is null)
                throw new ArgumentNullException(nameof(predicate));

            var count = 0;

            foreach (var ply in ExPlayer.Players)
            {
                if (!predicate(ply))
                    continue;

                if (player.AddReceiver(ply))
                    count++;
            }

            return count;
        }

        public static void AddAllReceivers(this AudioPlayer player)
        {
            if (player is null)
                throw new ArgumentNullException(nameof(player));

            foreach (var ply in ExPlayer.Players)
            {
                if (player.Receivers.Any(x => x.PlayerId == ply.PlayerId))
                    continue;

                player.AddReceiver(ply);
            }
        }
    }
}