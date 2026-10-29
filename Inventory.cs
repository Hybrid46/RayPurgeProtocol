using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PurgeProtocol
{
    public static class Inventory
    {
        public static int breadCount = 0;
        public static int mushroomCount = 0;

        public const int BREAD_HEAL = 10;
        public const int MUSHROOM_MANA = 20;

        public static void Reset()
        {
            breadCount = 0;
            mushroomCount = 0;
        }

        public static bool UseBread(Entity player)
        {
            if (breadCount <= 0) return false;
            if (player?.healthComponent == null) return false;
            if (player.healthComponent.current >= player.healthComponent.max) return false;

            player.healthComponent.TakeDamage(-BREAD_HEAL);
            breadCount--;
            return true;
        }

        public static bool UseMushroom(Entity player)
        {
            if (mushroomCount <= 0) return false;
            if (player?.manaComponent == null) return false;
            if (player.manaComponent.current >= player.manaComponent.max) return false;

            player.manaComponent.TakeMana(-MUSHROOM_MANA);
            mushroomCount--;
            return true;
        }
    }
}