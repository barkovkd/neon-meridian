using UnityEngine;

namespace NeonMeridian
{
    /// <summary>
    /// First person weapon models, assembled from primitives. They are built at real-world size under a root that is
    /// scaled down and held very close to the camera: the picture is identical, but the gun can never poke through a wall.
    /// </summary>
    public static class ViewModels
    {
        public const float Scale = 0.3f;

        public sealed class Built
        {
            public GameObject root;
            public Transform muzzle;
            public Vector3 hipPosition;
            public Vector3 aimPosition;
            public Material glow;        // emissive accent, pulsed by the weapon system
            public Color glowColor;
        }

        static Material frame, dark, sleeve, skin;

        static void EnsureMaterials()
        {
            if (frame != null) return;
            frame = Mats.Lit(new Color(0.4f, 0.42f, 0.48f), 0.8f, 0.6f);
            dark = Mats.Lit(new Color(0.17f, 0.18f, 0.21f), 0.5f, 0.5f);
            sleeve = Mats.Lit(new Color(0.2f, 0.21f, 0.25f), 0f, 0.35f);
            skin = Mats.Lit(new Color(0.27f, 0.27f, 0.31f), 0.6f, 0.55f);
        }

        static GameObject Part(Transform parent, Vector3 pos, Vector3 size, Material mat, Vector3 euler = default)
        {
            GameObject go = Prims.Box(parent, pos, size, mat);
            if (euler != default) go.transform.localRotation = Quaternion.Euler(euler);
            return go;
        }

        static void Barrel(Transform parent, Vector3 center, float diameter, float length, Material mat)
        {
            GameObject go = Prims.Rod(parent, center, diameter, length, mat);
            go.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        }

        /// <summary>Right forearm and hand on the grip, plus a supporting left hand for long guns.</summary>
        static void Arms(Transform root, bool twoHanded, float foreZ, Material glow)
        {
            Part(root, new Vector3(0f, -0.075f, -0.035f), new Vector3(0.062f, 0.085f, 0.075f), skin);
            Part(root, new Vector3(0.06f, -0.16f, -0.2f), new Vector3(0.078f, 0.08f, 0.34f), sleeve, new Vector3(-24f, -16f, 0f));
            Part(root, new Vector3(0.098f, -0.12f, -0.17f), new Vector3(0.006f, 0.012f, 0.2f), glow, new Vector3(-24f, -16f, 0f));
            if (!twoHanded) return;
            Part(root, new Vector3(-0.012f, -0.045f, foreZ), new Vector3(0.07f, 0.06f, 0.09f), skin);
            Part(root, new Vector3(-0.13f, -0.15f, foreZ - 0.17f), new Vector3(0.078f, 0.08f, 0.36f), sleeve, new Vector3(-26f, 32f, 0f));
        }

        static Built Begin(string name, Transform parent, Color color, float intensity)
        {
            EnsureMaterials();
            GameObject root = new GameObject(name);
            root.layer = Layers.ViewModel;
            root.transform.SetParent(parent, false);
            Built b = new Built { root = root, glowColor = color * intensity };
            b.glow = Mats.GlowInstance(b.glowColor);
            return b;
        }

        static void Finish(Built b, Vector3 muzzle)
        {
            b.muzzle = Prims.Empty(b.root.transform, muzzle, "Muzzle").transform;
            Layers.SetLayerRecursively(b.root, Layers.ViewModel);
            b.root.SetActive(false);
        }

        public static Built Pistol(Transform parent)
        {
            Built b = Begin("VM_Pistol", parent, new Color(0.2f, 0.9f, 1f), 3f);
            Transform r = b.root.transform;
            Part(r, new Vector3(0f, 0.032f, 0.065f), new Vector3(0.036f, 0.046f, 0.21f), frame);
            Part(r, new Vector3(0f, -0.004f, 0.05f), new Vector3(0.03f, 0.03f, 0.15f), dark);
            Part(r, new Vector3(0f, -0.068f, -0.03f), new Vector3(0.033f, 0.115f, 0.046f), dark, new Vector3(-13f, 0f, 0f));
            Part(r, new Vector3(0f, -0.022f, 0.02f), new Vector3(0.012f, 0.03f, 0.06f), dark);
            Part(r, new Vector3(0.0185f, 0.035f, 0.07f), new Vector3(0.002f, 0.007f, 0.13f), b.glow);
            Part(r, new Vector3(-0.0185f, 0.035f, 0.07f), new Vector3(0.002f, 0.007f, 0.13f), b.glow);
            Part(r, new Vector3(0f, 0.058f, -0.03f), new Vector3(0.006f, 0.006f, 0.006f), b.glow);
            Arms(r, false, 0f, b.glow);
            b.hipPosition = new Vector3(0.2f, -0.17f, 0.46f);
            b.aimPosition = new Vector3(0f, -0.075f, 0.36f);
            Finish(b, new Vector3(0f, 0.032f, 0.18f));
            return b;
        }

        public static Built Smg(Transform parent)
        {
            Built b = Begin("VM_Smg", parent, new Color(1f, 0.2f, 0.62f), 3f);
            Transform r = b.root.transform;
            Part(r, new Vector3(0f, 0.022f, 0.1f), new Vector3(0.05f, 0.072f, 0.34f), frame);
            Part(r, new Vector3(0f, 0.03f, 0.33f), new Vector3(0.036f, 0.036f, 0.16f), dark);
            Barrel(r, new Vector3(0f, 0.03f, 0.43f), 0.02f, 0.06f, frame);
            Part(r, new Vector3(0f, -0.1f, 0.11f), new Vector3(0.03f, 0.17f, 0.05f), dark, new Vector3(8f, 0f, 0f));
            Part(r, new Vector3(0f, -0.07f, -0.05f), new Vector3(0.035f, 0.1f, 0.046f), dark, new Vector3(-12f, 0f, 0f));
            Part(r, new Vector3(0f, 0.02f, -0.14f), new Vector3(0.03f, 0.04f, 0.16f), dark);
            Part(r, new Vector3(0f, 0.062f, 0.1f), new Vector3(0.012f, 0.006f, 0.26f), b.glow);
            Part(r, new Vector3(0.026f, 0.02f, 0.2f), new Vector3(0.002f, 0.02f, 0.1f), b.glow);
            Arms(r, true, 0.27f, b.glow);
            b.hipPosition = new Vector3(0.19f, -0.18f, 0.4f);
            b.aimPosition = new Vector3(0f, -0.088f, 0.3f);
            Finish(b, new Vector3(0f, 0.03f, 0.47f));
            return b;
        }

        public static Built Shotgun(Transform parent)
        {
            Built b = Begin("VM_Shotgun", parent, new Color(1f, 0.62f, 0.14f), 3f);
            Transform r = b.root.transform;
            Part(r, new Vector3(0f, 0.02f, 0.05f), new Vector3(0.056f, 0.078f, 0.3f), frame);
            Barrel(r, new Vector3(0f, 0.042f, 0.42f), 0.04f, 0.5f, dark);
            Barrel(r, new Vector3(0f, 0.002f, 0.36f), 0.03f, 0.36f, frame);
            Part(r, new Vector3(0f, -0.004f, 0.32f), new Vector3(0.052f, 0.046f, 0.16f), dark);
            Part(r, new Vector3(0f, 0f, -0.2f), new Vector3(0.045f, 0.07f, 0.25f), dark, new Vector3(6f, 0f, 0f));
            Part(r, new Vector3(0f, -0.07f, -0.04f), new Vector3(0.036f, 0.1f, 0.046f), dark, new Vector3(-14f, 0f, 0f));
            Part(r, new Vector3(0.029f, 0.03f, 0.06f), new Vector3(0.002f, 0.012f, 0.2f), b.glow);
            Part(r, new Vector3(-0.029f, 0.03f, 0.06f), new Vector3(0.002f, 0.012f, 0.2f), b.glow);
            Arms(r, true, 0.32f, b.glow);
            b.hipPosition = new Vector3(0.19f, -0.18f, 0.36f);
            b.aimPosition = new Vector3(0f, -0.092f, 0.28f);
            Finish(b, new Vector3(0f, 0.042f, 0.68f));
            return b;
        }

        public static Built Rail(Transform parent)
        {
            Built b = Begin("VM_Rail", parent, new Color(0.3f, 1f, 0.85f), 3.4f);
            Transform r = b.root.transform;
            Part(r, new Vector3(0f, 0.02f, 0.02f), new Vector3(0.06f, 0.082f, 0.36f), frame);
            Part(r, new Vector3(0.022f, 0.04f, 0.44f), new Vector3(0.012f, 0.032f, 0.6f), dark);
            Part(r, new Vector3(-0.022f, 0.04f, 0.44f), new Vector3(0.012f, 0.032f, 0.6f), dark);
            for (int i = 0; i < 5; i++)
                Part(r, new Vector3(0f, 0.04f, 0.24f + i * 0.11f), new Vector3(0.066f, 0.05f, 0.014f), b.glow);
            Barrel(r, new Vector3(0f, 0.095f, 0.06f), 0.038f, 0.22f, dark);
            Part(r, new Vector3(0f, 0.095f, 0.172f), new Vector3(0.026f, 0.026f, 0.004f), b.glow);
            Part(r, new Vector3(0f, 0.005f, -0.24f), new Vector3(0.046f, 0.075f, 0.24f), dark, new Vector3(5f, 0f, 0f));
            Part(r, new Vector3(0f, -0.07f, -0.05f), new Vector3(0.036f, 0.1f, 0.046f), dark, new Vector3(-14f, 0f, 0f));
            Arms(r, true, 0.26f, b.glow);
            b.hipPosition = new Vector3(0.19f, -0.19f, 0.34f);
            b.aimPosition = new Vector3(0f, -0.118f, 0.22f);
            Finish(b, new Vector3(0f, 0.04f, 0.76f));
            return b;
        }
    }
}
