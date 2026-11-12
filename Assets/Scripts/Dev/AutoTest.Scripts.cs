using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace NeonMeridian
{
    // The scenarios the test bot can run:
    //   tour   - fly-by of the district, one screenshot per viewpoint
    //   title  - the title screen
    //   play   - a few minutes of ordinary play: moving, shooting, scanner, grenade, vendor, pause
    //   story  - the whole campaign on fast-forward (teleports, instant kills) to prove the mission logic completes
    public sealed partial class AutoTest
    {
        IEnumerator ScriptByName(string name)
        {
            switch (name)
            {
                case "title": return TitleScreen();
                case "play": return Play();
                case "story": return Story();
                case "drive": return Drive();
                case "audio": return Audio();
                default: return null;
            }
        }

        // ------------------------------------------------------------------ bot helpers

        PlayerController Player => director.Player;

        void Release()
        {
            held = default;
        }

        IEnumerator Walk(Vector2 move, float seconds, bool sprint = false)
        {
            held.move = move;
            held.sprint = sprint;
            yield return Wait(seconds);
            held.move = Vector2.zero;
            held.sprint = false;
        }

        /// <summary>Keeps the crosshair on an enemy and pulls the trigger until it dies or time runs out.</summary>
        IEnumerator Engage(Enemy enemy, float seconds, bool head)
        {
            float end = botClock + seconds;
            float nextShot = 0f;
            while (enemy != null && enemy.IsAlive && botClock < end)
            {
                Player.LookAt(head ? enemy.HeadPoint - Vector3.up * 0.3f * enemy.Def.scale : enemy.AimPoint);
                held.fireHeld = true;
                if (botClock > nextShot)
                {
                    held.firePressed = true;
                    nextShot = botClock + 0.2f;
                }
                yield return null;
            }
            held.fireHeld = false;
        }

        static void Slay(Enemy enemy)
        {
            if (enemy != null && enemy.IsAlive)
                enemy.TakeDamage(new DamageInfo(1e6f, DamageKind.Bullet, enemy.AimPoint, Vector3.up, Team.Player));
        }

        static int SlayGroup(string group)
        {
            int n = 0;
            for (int i = Enemy.All.Count - 1; i >= 0; i--)
            {
                if (Enemy.All[i].Group != group) continue;
                Slay(Enemy.All[i]);
                n++;
            }
            return n;
        }

        static Interactable FindInteractable(string promptPart)
        {
            foreach (Interactable it in Interactable.All)
                if (it.prompt.Contains(promptPart)) return it;
            return null;
        }

        static int CountInteractables(string promptPart)
        {
            int n = 0;
            foreach (Interactable it in Interactable.All)
                if (it.prompt.Contains(promptPart)) n++;
            return n;
        }

        IEnumerator WaitFor(System.Func<bool> condition, float timeout, string what)
        {
            float end = botClock + timeout;
            while (!condition() && botClock < end) yield return null;
            if (!condition()) Note("TIMEOUT waiting for: " + what);
        }

        /// <summary>Stands the player in front of a spot, facing it.</summary>
        void Stand(Vector3 at, Vector3 offset)
        {
            Vector3 p = Navigation.Snap(at + offset, 6f);
            Vector3 to = at - p;
            Player.Teleport(p, Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg);
        }

        // ------------------------------------------------------------------ tour

        IEnumerator Tour()
        {
            City city = director.City;
            director.HudUi.SetVisible(false);
            Vector3 start = city.Poi("start");
            Vector3 market = city.Poi("market");
            Vector3 tower = city.Poi("tower");
            Vector3 hideout = city.Poi("hideout");
            int n = city.blocksPerSide;

            Look(start + Vector3.up * 1.7f, start + new Vector3(60f, 6f, -2f));
            yield return Wait(2.5f);
            yield return Capture("01_start_street");

            Look(start + new Vector3(20f, 1.7f, -6f), start + new Vector3(60f, 30f, 10f));
            yield return Wait(1.2f);
            yield return Capture("02_street_up");

            Look(market + new Vector3(-6f, 2.2f, -34f), market + new Vector3(0f, 3f, -10f));
            yield return Wait(1.5f);
            yield return Capture("03_market");

            Look(tower + new Vector3(-14f, 1.8f, -12f), tower + new Vector3(4f, 40f, 30f));
            yield return Wait(1.5f);
            yield return Capture("04_tower");

            Vector3 lights = city.Intersection(n - 2, 2);
            Look(lights + new Vector3(3f, 1.8f, -30f), lights + new Vector3(0f, 8f, 40f));
            yield return Wait(1.5f);
            yield return Capture("05_lights_street");

            Look(hideout + new Vector3(0f, 2f, -30f), hideout + new Vector3(0f, 3f, 0f));
            yield return Wait(1.5f);
            yield return Capture("06_hideout");

            Look(new Vector3(-150f, 150f, -190f), new Vector3(0f, 60f, 0f));
            yield return Wait(1.5f);
            yield return Capture("07_aerial");

            Vector3 x = city.Intersection(1, 1);
            Look(x + new Vector3(-4f, 1.7f, -4f), x + new Vector3(40f, 4f, 30f));
            yield return Wait(1.5f);
            yield return Capture("08_intersection");

            // a street lamp from close by: are light pools visible on the road?
            Vector3 lamp = city.Intersection(1, 1) + new Vector3(30f, 0f, 0f);
            Look(lamp + new Vector3(-6f, 3f, -7f), lamp + new Vector3(8f, 0f, 2f));
            yield return Wait(1.5f);
            yield return Capture("09_lamp_pool");

            Note("lights total: " + city.lights.Count + ", blocks: " + n * n);
        }

        IEnumerator TitleScreen()
        {
            yield return Wait(3f);
            yield return Capture("t01_title");
            held.downPressed = true;
            yield return Wait(0.2f);
            held.confirmPressed = true;
            yield return Wait(0.8f);
            yield return Capture("t02_controls");
            Note("menu after 'down, confirm': " + director.MenusUi.Current);

            held.cancelPressed = true;
            yield return Wait(0.4f);
            held.downPressed = true;
            yield return Wait(0.2f);
            held.downPressed = true;
            yield return Wait(0.2f);
            held.confirmPressed = true;
            yield return Wait(0.8f);
            yield return Capture("t03_settings");
            Note("menu after 'back, down, down, confirm': " + director.MenusUi.Current);
        }

        /// <summary>The bot cannot listen, so it measures: every generated clip must be present, finite and unclipped.</summary>
        IEnumerator Audio()
        {
            float limit = Time.realtimeSinceStartup + 40f;
            while ((!Sound.Ready || !Sound.MusicReady) && Time.realtimeSinceStartup < limit) yield return null;
            Note("effects ready: " + Sound.Ready + ", soundtrack ready: " + Sound.MusicReady);
            Note("clips:\n" + Sound.Describe());
        }

        // ------------------------------------------------------------------ play

        IEnumerator Play()
        {
            City city = director.City;
            yield return Wait(3.5f);
            yield return Capture("p01_start");

            yield return Walk(new Vector2(0f, 1f), 2.5f, true);
            held.jumpPressed = true;
            yield return Walk(new Vector2(0.4f, 1f), 1f, true);
            held.dashPressed = true;
            yield return Wait(0.5f);
            yield return Capture("p02_moving");

            // a fight: three marauders down the street
            Vector3 ahead = Player.transform.position + Player.transform.forward * 16f;
            List<Enemy> foes = new List<Enemy>();
            for (int i = 0; i < 3; i++)
                foes.Add(EnemyFactory.Spawn(i == 2 ? EnemyKind.Rusher : EnemyKind.Grunt, ahead + new Vector3(0f, 0f, (i - 1) * 3.5f), 270f, "test"));
            foreach (Enemy e in foes) e.Aggro(Player.transform.position);
            yield return Wait(1.2f);
            Player.LookAt(foes[0].AimPoint);
            yield return Wait(0.2f);
            yield return Capture("p03_enemies");

            StartCoroutine(Engage(foes[0], 6f, true));
            yield return Wait(0.6f);
            yield return Capture("p04_shooting");
            yield return WaitFor(() => !foes[0].IsAlive, 8f, "first marauder to die");

            // scanner on the next one, then an exploit
            if (foes[1].IsAlive)
            {
                Player.LookAt(foes[1].AimPoint);
                held.scannerHeld = true;
                yield return Wait(0.9f);
                Player.LookAt(foes[1].AimPoint);
                yield return Wait(0.1f);
                yield return Capture("p05_scanner");
                held.digitPressed = 1;
                yield return Wait(0.5f);
                held.scannerHeld = false;
                yield return Wait(0.4f);
                yield return Engage(foes[1], 6f, false);
            }

            if (foes[2].IsAlive)
            {
                Player.LookAt(foes[2].AimPoint);
                held.grenadePressed = true;
                yield return Wait(0.3f);
                held.meleePressed = true;
                yield return Engage(foes[2], 5f, false);
            }
            yield return Wait(1f);
            yield return Capture("p06_after_fight");
            Note("player hp after fight: " + Mathf.RoundToInt(Player.Stats.health) + ", shield " + Mathf.RoundToInt(Player.Stats.shield)
                 + ", kills " + Progress.Kills + ", credits " + Player.Stats.credits);
            SlayGroup("test");
            Player.invulnerable = true;

            // weapons: give everything and cycle
            director.Weapons.Give(WeaponSystem.Smg, 100);
            director.Weapons.Give(WeaponSystem.Shotgun, 20);
            director.Weapons.Give(WeaponSystem.Rail, 10);
            for (int slot = 2; slot <= 4; slot++)
            {
                held.digitPressed = slot;
                yield return Wait(0.8f);
                held.firePressed = true;
                held.fireHeld = true;
                yield return Wait(0.25f);
                held.fireHeld = false;
                yield return Wait(0.15f);
                yield return Capture("p07_weapon" + slot);
            }
            held.aimHeld = true;
            yield return Wait(0.6f);
            yield return Capture("p08_aim_rail");
            held.aimHeld = false;
            held.reloadPressed = true;
            yield return Wait(0.9f);
            yield return Capture("p09_reload");

            // drone and turret
            Enemy drone = EnemyFactory.Spawn(EnemyKind.Drone, Player.transform.position + Player.transform.forward * 14f + Vector3.up * 6f, 0f, "test");
            Enemy turret = EnemyFactory.Spawn(EnemyKind.Turret, Navigation.Snap(Player.transform.position + Player.transform.forward * 12f + Player.transform.right * 5f), 0f, "test");
            drone.Aggro(Player.transform.position);
            turret.Aggro(Player.transform.position);
            yield return Wait(1.6f);
            Player.LookAt(turret.AimPoint);
            yield return Wait(0.15f);
            yield return Capture("p10_drone_turret");
            // hijack the turret and let it shoot the drone
            Player.Stats.buffer = Player.Stats.maxBuffer;
            held.scannerHeld = true;
            yield return Wait(0.6f);
            Player.LookAt(turret.AimPoint);
            yield return Wait(0.1f);
            held.digitPressed = 3;
            yield return Wait(0.3f);
            held.scannerHeld = false;
            yield return Wait(3f);
            yield return Capture("p11_hijacked");
            Note("turret team after hijack: " + turret.Team + ", drone alive: " + drone.IsAlive);
            SlayGroup("test");

            // vendor and pause menus
            Stand(city.Poi("vendor"), new Vector3(0f, 0f, -2.2f));
            yield return Wait(1f);
            yield return Capture("p12_vendor_prompt");
            held.interactPressed = true;
            held.interactHeld = true;
            yield return Wait(0.3f);
            held.interactHeld = false;
            yield return Wait(0.6f);
            yield return Capture("p13_vendor_menu");
            Note("state at vendor: " + director.State);
            director.CloseVendor();
            yield return Wait(0.4f);

            director.Pause();
            yield return Wait(0.7f);
            yield return Capture("p14_pause");
            director.Resume();
            yield return Wait(0.3f);

            // death and respawn
            Player.invulnerable = false;
            Player.TakeDamage(new DamageInfo(1e5f, DamageKind.Explosion, Player.AimPoint, Vector3.forward, Team.Enemy));
            yield return Wait(3.2f);
            yield return Capture("p15_death");
            Note("state after death: " + director.State);
            held.confirmPressed = true;
            yield return Wait(1.5f);
            Note("state after respawn: " + director.State + ", hp " + Mathf.RoundToInt(Player.Stats.health));
            Release();
        }

        // ------------------------------------------------------------------ driving

        IEnumerator Drive()
        {
            City city = director.City;
            Player.invulnerable = true;
            yield return Wait(1.5f);
            Interactable door = FindInteractable("ГЛАЙДЕР");
            if (door == null)
            {
                Note("no car door found");
                yield break;
            }
            Vector3 carPos = door.transform.position;
            Stand(carPos, new Vector3(1.5f, 0f, 3.4f));
            Player.LookAt(carPos + Vector3.up * 0.8f);
            yield return Wait(0.8f);
            yield return Capture("d01_car_parked");

            held.interactPressed = true;
            held.interactHeld = true;
            yield return Wait(0.3f);
            held.interactHeld = false;
            yield return Wait(0.8f);
            PlayerCar car = PlayerCar.Active;
            Note("seated: " + Player.Seated + ", driving: " + (car != null));
            if (car == null) yield break;
            yield return Capture("d02_seated");

            // a long clear run east along the first street
            car.Place(city.Intersection(1, 1) + new Vector3(12f, 0f, -1.6f), 90f);
            yield return Wait(0.5f);
            Vector3 p0 = Player.transform.position;
            held.move = new Vector2(0f, 1f);
            yield return Wait(3f);
            yield return Capture("d03_driving");
            held.sprint = true;
            yield return Wait(2.5f);
            Note("top speed reached: " + Mathf.RoundToInt(car.SpeedKmh) + " km/h after " + Mathf.RoundToInt(Vector3.Distance(p0, Player.transform.position)) + " m");
            yield return Capture("d04_boost");
            held.sprint = false;
            held.move = new Vector2(1f, 0.6f);
            yield return Wait(1.3f);
            yield return Capture("d05_turning");
            held.move = new Vector2(0f, -1f);
            yield return Wait(2f);
            held.move = Vector2.zero;
            yield return Wait(0.6f);
            Note("stopped at " + Mathf.RoundToInt(car.SpeedKmh) + " km/h, " + Mathf.RoundToInt(Vector3.Distance(p0, Player.transform.position)) + " m from the start of the run");

            held.interactPressed = true;
            yield return Wait(1.2f);
            Note("after exit: seated " + Player.Seated + ", grounded " + Player.Grounded + ", state " + director.State);
            Player.LookAt(car.transform.position + Vector3.up * 0.8f);
            yield return Wait(0.4f);
            yield return Capture("d06_exited");
            Release();
        }

        // ------------------------------------------------------------------ story on fast-forward

        IEnumerator Story()
        {
            City city = director.City;
            MissionSystem m = director.Missions;
            WeaponSystem weapons = director.Weapons;

            // ---- act one
            Player.invulnerable = true;
            yield return Wait(1f);
            Stand(city.Poi("vendor"), new Vector3(-30f, 0f, -18f));
            yield return WaitFor(() => Enemy.CountAlive("ambush") > 0, 12f, "the ambush to spawn");
            yield return Wait(1.5f);
            Enemy first = Enemy.All.Count > 0 ? Enemy.All[0] : null;
            if (first != null) Player.LookAt(first.AimPoint);
            yield return Wait(0.3f);
            yield return Capture("s01_ambush");
            SlayGroup("ambush");
            yield return Wait(1f);
            weapons.Give(WeaponSystem.Smg, 144);
            yield return WaitFor(() => m.Act >= 2, 20f, "act two to begin");
            Note("act " + m.Act + " reached, level " + Player.Stats.level + ", credits " + Player.Stats.credits);

            // ---- act two
            Stand(city.Poi("hideoutGate"), new Vector3(0f, 0f, -6f));
            yield return WaitFor(() => Enemy.CountAlive("hideout") > 0, 12f, "the hideout to fill");
            yield return Wait(2f);
            Player.LookAt(city.Poi("hideoutCore") + Vector3.up * 1.5f);
            yield return Wait(0.3f);
            yield return Capture("s02_hideout_gate");
            Note("hideout enemies: " + Enemy.CountAlive("hideout"));
            // walk in and fight for real for a while
            Player.invulnerable = false;
            Stand(city.Poi("hideoutCore"), new Vector3(0f, 0f, -24f));
            yield return Wait(0.5f);
            for (int k = 0; k < 4; k++)
            {
                Enemy target = null;
                float best = float.MaxValue;
                foreach (Enemy e in Enemy.All)
                {
                    if (!e.IsAlive || e.Group != "hideout") continue;
                    float d = (e.transform.position - Player.transform.position).sqrMagnitude;
                    if (d < best)
                    {
                        best = d;
                        target = e;
                    }
                }
                if (target == null) break;
                yield return Engage(target, 4f, true);
                if (k == 1) yield return Capture("s03_hideout_fight");
            }
            Note("after 16 s of real fighting: hp " + Mathf.RoundToInt(Player.Stats.health) + ", hideout left " + Enemy.CountAlive("hideout"));
            Player.invulnerable = true;
            Player.Heal(1000f);
            SlayGroup("hideout");
            yield return Wait(1.2f);
            Stand(city.Poi("hideoutCore"), new Vector3(0f, 0f, -1.5f));
            yield return WaitFor(() => m.Act >= 3, 25f, "act three to begin");
            Note("act " + m.Act + " reached, shotgun owned: " + weapons.slots[WeaponSystem.Shotgun].owned);

            // ---- act three: three relays
            yield return Wait(1f);
            string[] keys = { "relayA", "relayB", "relayC" };
            for (int i = 0; i < 3; i++)
            {
                Vector3 console = city.Poi(keys[i] + "_console");
                Stand(console, new Vector3(0f, 0f, -14f));
                yield return WaitFor(() => Enemy.CountAlive("relay" + i) > 0, 10f, "relay guards " + i);
                yield return Wait(1.5f);
                if (i == 0) yield return Capture("s04_relay_guards");
                SlayGroup("relay" + i);
                Stand(console, new Vector3(0f, 0f, -1.3f));
                Player.LookAt(console + Vector3.up * 1.15f);
                held.interactHeld = true;
                held.interactPressed = true;
                if (i == 0)
                {
                    yield return Wait(2f);
                    yield return Capture("s05_relay_hacking");
                }
                int before = CountInteractables("РЕТРАНСЛЯТОР");
                yield return WaitFor(() => CountInteractables("РЕТРАНСЛЯТОР") < before, 9f, "relay " + i + " hack");
                held.interactHeld = false;
                yield return Wait(0.6f);
                SlayGroup("relay" + i);
            }
            yield return WaitFor(() => m.Act >= 4, 25f, "act four to begin");
            Note("act " + m.Act + " reached, rail owned: " + weapons.slots[WeaponSystem.Rail].owned);

            // ---- act four: the tower
            yield return Wait(1f);
            Stand(city.Poi("tower"), new Vector3(0f, 0f, -16f));
            yield return WaitFor(() => Enemy.CountAlive("tower") > 0, 12f, "tower guards");
            yield return Wait(2f);
            yield return Capture("s06_tower_guards");
            SlayGroup("tower");
            Vector3 gate = city.Poi("towerGate");
            Stand(gate, new Vector3(0f, 0f, -1.3f));
            Player.LookAt(gate + Vector3.up * 1.15f);
            held.interactHeld = true;
            held.interactPressed = true;
            yield return WaitFor(() => Enemy.CountAlive("boss") > 0, 10f, "the walker to appear");
            held.interactHeld = false;
            Stand(city.Poi("bossSpawn"), new Vector3(-9f, 0f, -18f));
            yield return Wait(2.5f);
            Enemy boss = null;
            foreach (Enemy e in Enemy.All)
                if (e.Def.kind == EnemyKind.Boss) boss = e;
            if (boss != null)
            {
                Player.LookAt(boss.AimPoint);
                yield return Wait(0.3f);
                yield return Capture("s07_boss");
                held.digitPressed = 4;
                yield return Wait(0.8f);
                yield return Engage(boss, 6f, false);
                yield return Capture("s08_boss_fight");
                Note("boss hp after 6 s of rail fire: " + Mathf.RoundToInt(boss.Health01 * 100f) + "%, player hp " + Mathf.RoundToInt(Player.Stats.health));
                Slay(boss);
            }
            SlayGroup("boss");
            yield return Wait(1.5f);
            Stand(city.Poi("bossSpawn"), new Vector3(0f, 0f, -1.2f));
            yield return Wait(2.5f);
            Stand(city.Poi("extraction"), new Vector3(0f, 0f, 14f));
            yield return Wait(5.5f);
            SlayGroup("chase");
            Player.Teleport(city.Poi("extraction") + new Vector3(1f, 0.2f, 1f), 180f);
            yield return WaitFor(() => m.Finished, 14f, "the story to finish");
            yield return Wait(1f);
            yield return Capture("s09_ending");
            Note("story finished: " + m.Finished + ", state " + director.State + ", kills " + Progress.Kills + ", level " + Player.Stats.level);
            Release();
        }
    }
}
