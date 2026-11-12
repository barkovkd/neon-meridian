using UnityEngine;

namespace NeonMeridian
{
    /// <summary>Physics layers (indices match ProjectSettings/TagManager.asset) and the masks built from them.</summary>
    public static class Layers
    {
        public const int Default = 0;
        public const int IgnoreRaycast = 2;
        public const int Ui = 5;          // Unity's built-in interface layer
        public const int World = 6;       // static city geometry
        public const int Player = 7;
        public const int Enemy = 8;
        public const int Prop = 9;        // loose physics debris
        public const int Pickup = 10;     // trigger volumes
        public const int Vehicle = 11;
        public const int Civilian = 12;
        public const int ViewModel = 13;  // first person weapon, never collides
        public const int Projectile = 14;
        public const int Glass = 15;

        public const int WorldMask = 1 << World;
        public const int SolidMask = (1 << Default) | (1 << World) | (1 << Vehicle) | (1 << Glass);

        /// <summary>Everything a bullet fired by the player can hit.</summary>
        public const int PlayerShotMask = SolidMask | (1 << Enemy) | (1 << Civilian) | (1 << Prop);

        /// <summary>Everything a bullet fired by an enemy can hit.</summary>
        public const int EnemyShotMask = SolidMask | (1 << Player) | (1 << Civilian);

        /// <summary>Things that block line of sight.</summary>
        public const int SightMask = (1 << Default) | (1 << World);

        public const int CharacterMask = (1 << Player) | (1 << Enemy) | (1 << Civilian);

        public static void SetupCollisionMatrix()
        {
            for (int i = 0; i < 32; i++)
            {
                Physics.IgnoreLayerCollision(ViewModel, i, true);
                Physics.IgnoreLayerCollision(Projectile, i, true);
            }

            // debris only bounces off the city, it never shoves characters around
            Physics.IgnoreLayerCollision(Prop, Player, true);
            Physics.IgnoreLayerCollision(Prop, Enemy, true);
            Physics.IgnoreLayerCollision(Prop, Civilian, true);
            Physics.IgnoreLayerCollision(Prop, Pickup, true);

            // pickups are triggers for the player only
            Physics.IgnoreLayerCollision(Pickup, Enemy, true);
            Physics.IgnoreLayerCollision(Pickup, Civilian, true);
            Physics.IgnoreLayerCollision(Pickup, Vehicle, true);
            Physics.IgnoreLayerCollision(Pickup, World, true);
            Physics.IgnoreLayerCollision(Pickup, Pickup, true);

            // agents steer around each other through the navmesh, their hit capsules do not need to collide
            Physics.IgnoreLayerCollision(Enemy, Enemy, true);
            Physics.IgnoreLayerCollision(Enemy, Civilian, true);
            Physics.IgnoreLayerCollision(Civilian, Civilian, true);
        }

        public static void SetLayerRecursively(GameObject root, int layer)
        {
            root.layer = layer;
            Transform t = root.transform;
            for (int i = 0; i < t.childCount; i++)
                SetLayerRecursively(t.GetChild(i).gameObject, layer);
        }
    }
}
