namespace NeonMeridian
{
    public enum Sfx
    {
        PistolShot, SmgShot, ShotgunShot, RailShot, EnemyShot, TurretShot, Rocket,
        ReloadOut, ReloadIn, Empty, Melee, GrenadeThrow, Explosion,
        Step, Jump, Land, Dash,
        Hit, Headshot, Kill, Hurt, ShieldDown, ShieldUp, Heal, Death,
        EnemyAlert, DroneHum, BossStomp,
        Pickup, Buy, Deny, UiClick, UiHover, UiOpen,
        ScannerOn, ScannerOff, HackOk, Objective, LevelUp,
        Thunder, RainLoop, CityLoop, EngineLoop,
        Count
    }

    /// <summary>Recipes for every sound effect. Each call renders one mono buffer at 44.1 kHz.</summary>
    public static class SfxBank
    {
        public static bool IsLoop(Sfx id)
        {
            return id == Sfx.DroneHum || id == Sfx.RainLoop || id == Sfx.CityLoop || id == Sfx.EngineLoop;
        }

        public static float[] Render(Sfx id, Rng rng)
        {
            float[] d;
            switch (id)
            {
                case Sfx.PistolShot:
                    d = Synth.Buffer(0.4f);
                    Synth.AddNoise(d, rng, 0f, 0.25f, 1f, 7000f, 900f, 0f, 0.001f, 0.05f);
                    Synth.AddOsc(d, Wave.Sine, 0f, 0.14f, 190f, 55f, 0.9f, 0.001f, 0.05f);
                    Synth.AddOsc(d, Wave.Square, 0f, 0.03f, 1400f, 500f, 0.25f, 0.0005f, 0.01f);
                    Synth.AddNoise(d, rng, 0.03f, 0.35f, 0.16f, 2500f, 500f, 200f, 0.01f, 0.12f);
                    Synth.Drive(d, 1.6f);
                    break;

                case Sfx.SmgShot:
                    d = Synth.Buffer(0.24f);
                    Synth.AddNoise(d, rng, 0f, 0.14f, 1f, 9000f, 1500f, 300f, 0.0008f, 0.03f);
                    Synth.AddOsc(d, Wave.Sine, 0f, 0.08f, 240f, 90f, 0.7f, 0.001f, 0.03f);
                    Synth.AddOsc(d, Wave.Square, 0f, 0.02f, 2000f, 800f, 0.2f, 0.0005f, 0.008f);
                    Synth.Drive(d, 1.5f);
                    break;

                case Sfx.ShotgunShot:
                    d = Synth.Buffer(0.75f);
                    Synth.AddNoise(d, rng, 0f, 0.5f, 1f, 5500f, 300f, 0f, 0.001f, 0.12f);
                    Synth.AddOsc(d, Wave.Sine, 0f, 0.3f, 130f, 38f, 1f, 0.001f, 0.1f);
                    Synth.AddNoise(d, rng, 0.05f, 0.65f, 0.22f, 1800f, 300f, 100f, 0.02f, 0.22f);
                    Synth.Drive(d, 2f);
                    break;

                case Sfx.RailShot:
                    d = Synth.Buffer(0.9f);
                    Synth.AddOsc(d, Wave.Saw, 0f, 0.35f, 3200f, 160f, 0.6f, 0.001f, 0.12f);
                    Synth.AddOsc(d, Wave.Sine, 0f, 0.5f, 90f, 40f, 0.9f, 0.002f, 0.18f);
                    Synth.AddNoise(d, rng, 0f, 0.2f, 0.8f, 12000f, 2000f, 800f, 0.0005f, 0.04f);
                    Synth.AddOsc(d, Wave.Sine, 0.02f, 0.8f, 1250f, 1180f, 0.14f, 0.01f, 0.3f);
                    Synth.Echo(d, 0.09f, 0.4f, 0.5f);
                    Synth.Drive(d, 1.5f);
                    break;

                case Sfx.EnemyShot:
                    d = Synth.Buffer(0.3f);
                    Synth.AddOsc(d, Wave.Saw, 0f, 0.2f, 1100f, 280f, 0.6f, 0.002f, 0.07f);
                    Synth.AddOsc(d, Wave.Square, 0f, 0.2f, 1108f, 284f, 0.3f, 0.002f, 0.06f);
                    Synth.AddNoise(d, rng, 0f, 0.08f, 0.4f, 6000f, 2000f, 600f, 0.001f, 0.025f);
                    Synth.LowPass(d, 5000f);
                    break;

                case Sfx.TurretShot:
                    d = Synth.Buffer(0.18f);
                    Synth.AddOsc(d, Wave.Square, 0f, 0.1f, 760f, 420f, 0.6f, 0.001f, 0.035f);
                    Synth.AddNoise(d, rng, 0f, 0.07f, 0.6f, 8000f, 2500f, 500f, 0.0008f, 0.02f);
                    Synth.LowPass(d, 6000f);
                    break;

                case Sfx.Rocket:
                    d = Synth.Buffer(0.7f);
                    Synth.AddNoise(d, rng, 0f, 0.7f, 1f, 900f, 3500f, 150f, 0.03f, 0.35f);
                    Synth.AddOsc(d, Wave.Saw, 0f, 0.6f, 70f, 140f, 0.4f, 0.02f, 0.3f);
                    break;

                case Sfx.ReloadOut:
                    d = Synth.Buffer(0.28f);
                    Synth.AddNoise(d, rng, 0f, 0.03f, 1f, 4500f, 2500f, 900f, 0.0005f, 0.01f);
                    Synth.AddNoise(d, rng, 0.11f, 0.08f, 0.6f, 2400f, 900f, 300f, 0.002f, 0.03f);
                    Synth.AddOsc(d, Wave.Sine, 0.11f, 0.06f, 420f, 300f, 0.3f, 0.001f, 0.02f);
                    break;

                case Sfx.ReloadIn:
                    d = Synth.Buffer(0.36f);
                    Synth.AddNoise(d, rng, 0f, 0.04f, 0.8f, 3000f, 1500f, 400f, 0.0005f, 0.014f);
                    Synth.AddOsc(d, Wave.Sine, 0f, 0.05f, 520f, 380f, 0.3f, 0.001f, 0.02f);
                    Synth.AddNoise(d, rng, 0.17f, 0.03f, 1f, 6000f, 3000f, 1200f, 0.0004f, 0.009f);
                    Synth.AddNoise(d, rng, 0.21f, 0.05f, 0.9f, 5000f, 2000f, 900f, 0.0004f, 0.014f);
                    break;

                case Sfx.Empty:
                    d = Synth.Buffer(0.08f);
                    Synth.AddNoise(d, rng, 0f, 0.02f, 0.8f, 5000f, 3000f, 1500f, 0.0004f, 0.006f);
                    Synth.AddOsc(d, Wave.Sine, 0f, 0.03f, 900f, 700f, 0.25f, 0.0005f, 0.01f);
                    break;

                case Sfx.Melee:
                    d = Synth.Buffer(0.45f);
                    Synth.AddNoise(d, rng, 0f, 0.22f, 0.9f, 1200f, 7000f, 600f, 0.05f, 0.08f);
                    Synth.AddOsc(d, Wave.Sine, 0.08f, 0.35f, 3300f, 3180f, 0.22f, 0.002f, 0.12f);
                    Synth.AddOsc(d, Wave.Sine, 0.08f, 0.35f, 4950f, 4800f, 0.1f, 0.002f, 0.09f);
                    break;

                case Sfx.GrenadeThrow:
                    d = Synth.Buffer(0.3f);
                    Synth.AddNoise(d, rng, 0f, 0.28f, 0.8f, 700f, 3200f, 300f, 0.08f, 0.1f);
                    break;

                case Sfx.Explosion:
                    d = Synth.Buffer(1.7f);
                    Synth.AddNoise(d, rng, 0f, 1.5f, 1f, 4500f, 140f, 0f, 0.002f, 0.4f);
                    Synth.AddOsc(d, Wave.Sine, 0f, 0.9f, 85f, 28f, 1.1f, 0.002f, 0.3f);
                    Synth.AddNoise(d, rng, 0.08f, 1.5f, 0.3f, 900f, 200f, 60f, 0.05f, 0.6f);
                    Synth.Drive(d, 2.2f);
                    break;

                case Sfx.Step:
                    d = Synth.Buffer(0.16f);
                    Synth.AddNoise(d, rng, 0f, 0.08f, 1f, 600f, 200f, 0f, 0.002f, 0.03f);
                    Synth.AddNoise(d, rng, 0.01f, 0.12f, 0.22f, 7000f, 3000f, 2500f, 0.004f, 0.04f);
                    break;

                case Sfx.Jump:
                    d = Synth.Buffer(0.2f);
                    Synth.AddNoise(d, rng, 0f, 0.18f, 0.7f, 500f, 2200f, 250f, 0.03f, 0.07f);
                    break;

                case Sfx.Land:
                    d = Synth.Buffer(0.25f);
                    Synth.AddNoise(d, rng, 0f, 0.14f, 1f, 800f, 150f, 0f, 0.002f, 0.05f);
                    Synth.AddOsc(d, Wave.Sine, 0f, 0.12f, 95f, 45f, 0.8f, 0.002f, 0.05f);
                    break;

                case Sfx.Dash:
                    d = Synth.Buffer(0.42f);
                    Synth.AddNoise(d, rng, 0f, 0.38f, 1f, 400f, 5200f, 250f, 0.07f, 0.12f);
                    Synth.AddOsc(d, Wave.Saw, 0f, 0.3f, 320f, 70f, 0.3f, 0.01f, 0.1f);
                    Synth.LowPass(d, 7000f);
                    break;

                case Sfx.Hit:
                    d = Synth.Buffer(0.06f);
                    Synth.AddOsc(d, Wave.Sine, 0f, 0.04f, 1900f, 1500f, 0.8f, 0.0005f, 0.012f);
                    Synth.AddNoise(d, rng, 0f, 0.01f, 0.3f, 8000f, 4000f, 2000f, 0.0003f, 0.004f);
                    break;

                case Sfx.Headshot:
                    d = Synth.Buffer(0.3f);
                    Synth.AddOsc(d, Wave.Sine, 0f, 0.28f, 2637f, 2637f, 0.6f, 0.0005f, 0.07f);
                    Synth.AddOsc(d, Wave.Sine, 0f, 0.28f, 3951f, 3951f, 0.3f, 0.0005f, 0.05f);
                    Synth.AddNoise(d, rng, 0f, 0.012f, 0.4f, 9000f, 5000f, 2500f, 0.0003f, 0.004f);
                    break;

                case Sfx.Kill:
                    d = Synth.Buffer(0.32f);
                    Synth.AddOsc(d, Wave.Triangle, 0f, 0.1f, 1320f, 1320f, 0.6f, 0.001f, 0.04f);
                    Synth.AddOsc(d, Wave.Triangle, 0.08f, 0.2f, 1760f, 1760f, 0.6f, 0.001f, 0.07f);
                    break;

                case Sfx.Hurt:
                    d = Synth.Buffer(0.24f);
                    Synth.AddNoise(d, rng, 0f, 0.16f, 1f, 1600f, 300f, 0f, 0.002f, 0.05f);
                    Synth.AddOsc(d, Wave.Sine, 0f, 0.16f, 150f, 60f, 0.8f, 0.002f, 0.06f);
                    break;

                case Sfx.ShieldDown:
                    d = Synth.Buffer(0.5f);
                    Synth.AddOsc(d, Wave.Saw, 0f, 0.45f, 520f, 70f, 0.6f, 0.002f, 0.2f);
                    Synth.AddNoise(d, rng, 0f, 0.3f, 0.4f, 6000f, 800f, 400f, 0.001f, 0.08f);
                    Synth.LowPass(d, 4000f);
                    break;

                case Sfx.ShieldUp:
                    d = Synth.Buffer(0.55f);
                    Synth.AddOsc(d, Wave.Sine, 0f, 0.5f, 300f, 940f, 0.5f, 0.15f, 0.25f);
                    Synth.AddOsc(d, Wave.Triangle, 0.2f, 0.3f, 1880f, 1880f, 0.2f, 0.01f, 0.1f);
                    break;

                case Sfx.Heal:
                    d = Synth.Buffer(0.7f);
                    Synth.AddNoise(d, rng, 0f, 0.3f, 0.5f, 3000f, 9000f, 2000f, 0.02f, 0.1f);
                    Synth.AddOsc(d, Wave.Sine, 0.1f, 0.5f, 660f, 990f, 0.4f, 0.05f, 0.2f);
                    Synth.AddOsc(d, Wave.Sine, 0.25f, 0.4f, 1320f, 1320f, 0.25f, 0.01f, 0.15f);
                    break;

                case Sfx.Death:
                    d = Synth.Buffer(1.6f);
                    Synth.AddOsc(d, Wave.Saw, 0f, 1.5f, 220f, 36f, 0.7f, 0.01f, 0.6f);
                    Synth.AddOsc(d, Wave.Sine, 0f, 1.5f, 110f, 30f, 0.8f, 0.01f, 0.7f);
                    Synth.AddNoise(d, rng, 0f, 1.2f, 0.3f, 3000f, 200f, 0f, 0.01f, 0.4f);
                    Synth.LowPass(d, 2500f);
                    break;

                case Sfx.EnemyAlert:
                    d = Synth.Buffer(0.3f);
                    Synth.AddOsc(d, Wave.Square, 0f, 0.09f, 700f, 760f, 0.45f, 0.003f, 0.05f);
                    Synth.AddOsc(d, Wave.Square, 0.12f, 0.12f, 950f, 1040f, 0.45f, 0.003f, 0.06f);
                    Synth.LowPass(d, 4500f);
                    break;

                case Sfx.DroneHum:
                    d = Synth.Buffer(1.25f);
                    Synth.AddOsc(d, Wave.Saw, 0f, 1.25f, 110f, 110f, 0.4f, 0.01f, 100f);
                    Synth.AddOsc(d, Wave.Saw, 0f, 1.25f, 221f, 221f, 0.25f, 0.01f, 100f);
                    Synth.AddOsc(d, Wave.Square, 0f, 1.25f, 56f, 56f, 0.2f, 0.01f, 100f);
                    for (int i = 0; i < d.Length; i++) d[i] *= 0.7f + 0.3f * (float)System.Math.Sin(i / (float)Synth.Rate * 6.2831853f * 32f);
                    Synth.LowPass(d, 1800f);
                    d = Synth.MakeLoop(d, 0.25f);
                    break;

                case Sfx.BossStomp:
                    d = Synth.Buffer(1f);
                    Synth.AddOsc(d, Wave.Sine, 0f, 0.7f, 70f, 24f, 1.2f, 0.003f, 0.25f);
                    Synth.AddNoise(d, rng, 0f, 0.5f, 0.8f, 1500f, 150f, 0f, 0.003f, 0.15f);
                    Synth.Drive(d, 2f);
                    break;

                case Sfx.Pickup:
                    d = Synth.Buffer(0.34f);
                    Synth.AddOsc(d, Wave.Sine, 0f, 0.1f, 880f, 880f, 0.5f, 0.002f, 0.05f);
                    Synth.AddOsc(d, Wave.Sine, 0.07f, 0.1f, 1108.7f, 1108.7f, 0.5f, 0.002f, 0.05f);
                    Synth.AddOsc(d, Wave.Sine, 0.14f, 0.2f, 1318.5f, 1318.5f, 0.5f, 0.002f, 0.08f);
                    break;

                case Sfx.Buy:
                    d = Synth.Buffer(0.4f);
                    Synth.AddOsc(d, Wave.Triangle, 0f, 0.12f, 1568f, 1568f, 0.5f, 0.001f, 0.05f);
                    Synth.AddOsc(d, Wave.Triangle, 0.1f, 0.3f, 2093f, 2093f, 0.5f, 0.001f, 0.11f);
                    Synth.AddNoise(d, rng, 0f, 0.02f, 0.3f, 9000f, 6000f, 3000f, 0.0004f, 0.006f);
                    break;

                case Sfx.Deny:
                    d = Synth.Buffer(0.3f);
                    Synth.AddOsc(d, Wave.Square, 0f, 0.1f, 160f, 150f, 0.5f, 0.003f, 0.06f);
                    Synth.AddOsc(d, Wave.Square, 0.14f, 0.14f, 130f, 120f, 0.5f, 0.003f, 0.08f);
                    Synth.LowPass(d, 2200f);
                    break;

                case Sfx.UiClick:
                    d = Synth.Buffer(0.06f);
                    Synth.AddOsc(d, Wave.Sine, 0f, 0.04f, 1250f, 1750f, 0.6f, 0.0005f, 0.014f);
                    break;

                case Sfx.UiHover:
                    d = Synth.Buffer(0.04f);
                    Synth.AddOsc(d, Wave.Sine, 0f, 0.03f, 950f, 1000f, 0.35f, 0.0008f, 0.01f);
                    break;

                case Sfx.UiOpen:
                    d = Synth.Buffer(0.2f);
                    Synth.AddOsc(d, Wave.Triangle, 0f, 0.16f, 420f, 1300f, 0.5f, 0.01f, 0.07f);
                    Synth.AddNoise(d, rng, 0f, 0.1f, 0.2f, 3000f, 8000f, 1500f, 0.01f, 0.04f);
                    break;

                case Sfx.ScannerOn:
                    d = Synth.Buffer(0.36f);
                    Synth.AddOsc(d, Wave.Sine, 0f, 0.3f, 480f, 1600f, 0.4f, 0.02f, 0.14f);
                    Synth.AddNoise(d, rng, 0f, 0.3f, 0.35f, 800f, 9000f, 500f, 0.06f, 0.1f);
                    break;

                case Sfx.ScannerOff:
                    d = Synth.Buffer(0.3f);
                    Synth.AddOsc(d, Wave.Sine, 0f, 0.25f, 1500f, 420f, 0.4f, 0.005f, 0.1f);
                    Synth.AddNoise(d, rng, 0f, 0.2f, 0.25f, 8000f, 700f, 400f, 0.005f, 0.07f);
                    break;

                case Sfx.HackOk:
                    d = Synth.Buffer(0.6f);
                    for (int i = 0; i < 12; i++)
                    {
                        float f = 500f + rng.Range(0f, 2200f);
                        Synth.AddOsc(d, Wave.Square, i * 0.026f, 0.024f, f, f, 0.3f, 0.001f, 0.02f);
                    }
                    Synth.AddOsc(d, Wave.Sine, 0.33f, 0.26f, 1760f, 1760f, 0.5f, 0.002f, 0.1f);
                    Synth.AddOsc(d, Wave.Sine, 0.33f, 0.26f, 2637f, 2637f, 0.3f, 0.002f, 0.08f);
                    Synth.LowPass(d, 6000f);
                    break;

                case Sfx.Objective:
                    d = Synth.Buffer(1.3f);
                    foreach (float f in new[] { 440f, 554.4f, 659.3f, 880f })
                    {
                        Synth.AddOsc(d, Wave.Triangle, 0f, 1.2f, f, f, 0.3f, 0.004f, 0.4f);
                        Synth.AddOsc(d, Wave.Sine, 0f, 1.2f, f * 2.005f, f * 2.005f, 0.1f, 0.004f, 0.25f);
                    }
                    Synth.Echo(d, 0.19f, 0.35f, 0.6f);
                    break;

                case Sfx.LevelUp:
                    d = Synth.Buffer(1.1f);
                    float[] notes = { 523.3f, 659.3f, 784f, 1046.5f, 1318.5f };
                    for (int i = 0; i < notes.Length; i++)
                        Synth.AddOsc(d, Wave.Triangle, i * 0.09f, 0.5f, notes[i], notes[i], 0.4f, 0.003f, 0.16f);
                    Synth.Echo(d, 0.14f, 0.3f, 0.5f);
                    break;

                case Sfx.Thunder:
                    d = Synth.Buffer(4.5f);
                    Synth.AddNoise(d, rng, 0f, 0.5f, 1f, 2500f, 200f, 0f, 0.004f, 0.15f);
                    Synth.AddNoise(d, rng, 0.1f, 4.3f, 0.9f, 260f, 70f, 0f, 0.2f, 1.3f);
                    Synth.AddNoise(d, rng, 0.9f, 3f, 0.5f, 180f, 60f, 0f, 0.3f, 0.9f);
                    Synth.AddOsc(d, Wave.Sine, 0f, 2.5f, 48f, 30f, 0.5f, 0.05f, 0.9f);
                    Synth.Drive(d, 1.6f);
                    break;

                case Sfx.RainLoop:
                    d = Synth.Buffer(5f);
                    Synth.AddNoise(d, rng, 0f, 5f, 1f, 9000f, 9000f, 1400f, 0.01f, 1000f);
                    Synth.AddNoise(d, rng, 0f, 5f, 0.5f, 1200f, 1200f, 300f, 0.01f, 1000f);
                    d = Synth.MakeLoop(d, 0.8f);
                    break;

                case Sfx.CityLoop:
                    d = Synth.Buffer(8f);
                    Synth.AddNoise(d, rng, 0f, 8f, 1f, 150f, 150f, 0f, 0.01f, 1000f);
                    Synth.AddOsc(d, Wave.Sine, 0f, 8f, 55f, 55f, 0.2f, 0.5f, 1000f);
                    Synth.AddOsc(d, Wave.Sine, 0f, 8f, 82.6f, 82.4f, 0.12f, 0.5f, 1000f);
                    Synth.AddOsc(d, Wave.Saw, 0f, 8f, 110.3f, 110.1f, 0.03f, 0.5f, 1000f);
                    for (int i = 0; i < d.Length; i++)
                        d[i] *= 0.75f + 0.25f * (float)System.Math.Sin(i / (float)Synth.Rate * 6.2831853f * 0.125f);
                    Synth.LowPass(d, 900f);
                    d = Synth.MakeLoop(d, 1.5f);
                    break;

                case Sfx.EngineLoop:
                    d = Synth.Buffer(1.5f);
                    Synth.AddOsc(d, Wave.Saw, 0f, 1.5f, 72f, 72f, 0.5f, 0.01f, 1000f);
                    Synth.AddOsc(d, Wave.Square, 0f, 1.5f, 36f, 36f, 0.4f, 0.01f, 1000f);
                    Synth.AddOsc(d, Wave.Saw, 0f, 1.5f, 144.6f, 144.6f, 0.2f, 0.01f, 1000f);
                    Synth.AddNoise(d, rng, 0f, 1.5f, 0.15f, 2500f, 2500f, 400f, 0.01f, 1000f);
                    Synth.LowPass(d, 1500f);
                    d = Synth.MakeLoop(d, 0.3f);
                    break;

                default:
                    d = Synth.Buffer(0.05f);
                    break;
            }

            Synth.Normalize(d, 0.92f);
            if (!IsLoop(id)) Synth.FadeEdges(d);
            return d;
        }
    }
}
