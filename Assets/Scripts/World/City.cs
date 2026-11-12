using System.Collections.Generic;
using UnityEngine;

namespace NeonMeridian
{
    public enum BlockKind { Towers, Midrise, Industrial, Plaza, Market, Hideout, Megatower }

    public enum District { LowerTier, RustYard, Vertical, Lights }

    public sealed class BuildingInfo
    {
        public Rect footprint;   // x -> world X, y -> world Z
        public float height;
        public FacadeStyle style;
    }

    public sealed class BlockInfo
    {
        public int ix, iz;
        public BlockKind kind;
        public District district;
        public Vector3 center;
        public Rect rect;        // the lot (without sidewalks)
        public Transform root;
        public readonly List<BuildingInfo> buildings = new List<BuildingInfo>();
    }

    /// <summary>A light the city owns. Lights far from the camera are switched off by <see cref="CityLights"/>.</summary>
    public sealed class CityLight
    {
        public Light light;
        public Vector3 position;
        public float baseIntensity;
        public float flicker;    // 0 = steady, > 0 = neon buzz
        public float phase;
    }

    /// <summary>
    /// The generated district: its dimensions, blocks, points of interest and the lists other systems need
    /// (lights, cover spots, spawn points). Built by <see cref="CityGenerator"/>.
    /// </summary>
    public sealed class City
    {
        public int seed = 7;
        public int blocksPerSide = 7;
        public float blockSize = 64f;
        public float streetWidth = 20f;
        public float sidewalkWidth = 3.5f;
        public float curbHeight = 0.15f;

        public Transform root;
        public Transform lightsRoot;
        public BlockInfo[,] blocks;
        public Texture2D minimap;

        public readonly List<CityLight> lights = new List<CityLight>();
        public readonly Dictionary<string, Vector3> poi = new Dictionary<string, Vector3>();
        public readonly List<Vector3> streetSpawns = new List<Vector3>();   // sidewalk points good for ambient spawns
        public readonly List<Vector3> steamVents = new List<Vector3>();
        public readonly List<Bounds> parkedCars = new List<Bounds>();
        public readonly List<Vector4> holoSpots = new List<Vector4>();      // xyz position, w = size of the hologram
        public readonly List<Vector3> barrelSpots = new List<Vector3>();    // where explosive barrels belong

        public float Pitch => blockSize + streetWidth;
        public float HalfSize => (blocksPerSide * blockSize + (blocksPerSide + 1) * streetWidth) * 0.5f;
        public float RoadHalfWidth => streetWidth * 0.5f - sidewalkWidth;

        /// <summary>Centre line of street <paramref name="i"/> (0 .. blocksPerSide), measured along X or Z.</summary>
        public float StreetCenter(int i) => -HalfSize + streetWidth * 0.5f + i * Pitch;

        public float BlockCenterCoord(int j) => -HalfSize + streetWidth + blockSize * 0.5f + j * Pitch;

        public Vector3 BlockCenter(int ix, int iz) => new Vector3(BlockCenterCoord(ix), 0f, BlockCenterCoord(iz));

        public Vector3 Intersection(int ix, int iz) => new Vector3(StreetCenter(ix), 0f, StreetCenter(iz));

        public Vector3 Poi(string key)
        {
            return poi.TryGetValue(key, out Vector3 p) ? p : Vector3.zero;
        }

        public District DistrictOf(int ix, int iz)
        {
            int mid = blocksPerSide / 2;
            if (ix < mid && iz < mid) return District.LowerTier;
            if (ix < mid && iz > mid) return District.RustYard;
            if (ix > mid && iz < mid) return District.Lights;
            return District.Vertical;
        }

        public District DistrictAt(Vector3 p)
        {
            int ix = Mathf.Clamp(Mathf.FloorToInt((p.x + HalfSize - streetWidth * 0.5f) / Pitch), 0, blocksPerSide - 1);
            int iz = Mathf.Clamp(Mathf.FloorToInt((p.z + HalfSize - streetWidth * 0.5f) / Pitch), 0, blocksPerSide - 1);
            return DistrictOf(ix, iz);
        }

        public static string DistrictName(District d)
        {
            switch (d)
            {
                case District.LowerTier: return "НИЖНИЙ ЯРУС";
                case District.RustYard: return "РЖАВЫЙ ДВОР";
                case District.Lights: return "КВАРТАЛ ОГНЕЙ";
                default: return "ВЕРТИКАЛЬ";
            }
        }

        /// <summary>True when the point is over a roadway (not a sidewalk or a block).</summary>
        public bool IsOnRoad(Vector3 p)
        {
            float half = RoadHalfWidth;
            for (int i = 0; i <= blocksPerSide; i++)
            {
                float s = StreetCenter(i);
                if (Mathf.Abs(p.x - s) < half || Mathf.Abs(p.z - s) < half) return true;
            }
            return false;
        }

        public bool InsidePlayArea(Vector3 p, float margin = 0f)
        {
            float h = HalfSize - margin;
            return Mathf.Abs(p.x) < h && Mathf.Abs(p.z) < h;
        }
    }
}
