using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace NeonMeridian
{
    /// <summary>
    /// The story: four jobs chained together, each written as a coroutine that waits for the player to do things.
    /// Also owns the respawn checkpoint.
    /// </summary>
    public sealed class MissionSystem : MonoBehaviour
    {
        public static MissionSystem Instance { get; private set; }

        public int Act { get; private set; }
        public bool Finished { get; private set; }
        public Vector3 Checkpoint { get; private set; }
        public float CheckpointYaw { get; private set; }

        const string Vesta = "ВЕСТА";

        GameDirector game;
        City city;
        Hud hud;
        PlayerController player;
        WeaponSystem weapons;
        readonly WaitForSeconds tick = new WaitForSeconds(0.25f);

        public static MissionSystem Create(GameDirector game, City city, Hud hud, PlayerController player, WeaponSystem weapons)
        {
            GameObject go = new GameObject("Missions");
            MissionSystem m = go.AddComponent<MissionSystem>();
            m.game = game;
            m.city = city;
            m.hud = hud;
            m.player = player;
            m.weapons = weapons;
            Instance = m;
            m.SetCheckpoint(city.Poi("start"), 90f);
            return m;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>Starts the story. A later act can be chosen for testing; the gear earned before it is granted.</summary>
        public void Begin(int fromAct = 1)
        {
            if (fromAct > 1) weapons.Give(WeaponSystem.Smg, 180);
            if (fromAct > 2) weapons.Give(WeaponSystem.Shotgun, 36);
            if (fromAct > 3) weapons.Give(WeaponSystem.Rail, 16);
            if (fromAct > 1) player.Stats.credits += 200 * (fromAct - 1);
            StartCoroutine(Story(fromAct));
        }

        IEnumerator Story(int from)
        {
            if (from <= 1) yield return ActOne();
            if (from <= 2) yield return ActTwo();
            if (from <= 3) yield return ActThree();
            if (from <= 4) yield return ActFour();
        }

        // ------------------------------------------------------------------ helpers

        bool Near(Vector3 p, float radius)
        {
            Vector3 d = player.transform.position - p;
            d.y = 0f;
            return d.sqrMagnitude < radius * radius;
        }

        void Objective(string title, string text, Vector3? where)
        {
            hud.SetObjective(title, text, where);
        }

        void Say(string text)
        {
            hud.Say(Vesta, text);
        }

        void SetCheckpoint(Vector3 position, float yaw)
        {
            Checkpoint = Navigation.Snap(position, 6f);
            CheckpointYaw = yaw;
        }

        Enemy Spawn(EnemyKind kind, Vector3 around, float radius, string group)
        {
            Vector2 c = Random.insideUnitCircle * radius;
            Vector3 p = around + new Vector3(c.x, 0f, c.y);
            Vector3 toPlayer = player.transform.position - p;
            float yaw = Mathf.Atan2(toPlayer.x, toPlayer.z) * Mathf.Rad2Deg;
            return EnemyFactory.Spawn(kind, p, yaw, group);
        }

        Enemy SpawnDrone(Vector3 around, float radius, string group)
        {
            Vector2 c = Random.insideUnitCircle * radius;
            return EnemyFactory.Spawn(EnemyKind.Drone, around + new Vector3(c.x, Random.Range(6f, 9f), c.y), 0f, group);
        }

        /// <summary>Keeps the objective pointing at the nearest survivor of a group until the group is gone.</summary>
        IEnumerator GroupDead(string group, string title, string format)
        {
            while (true)
            {
                int alive = 0;
                Enemy nearest = null;
                float best = float.MaxValue;
                for (int i = 0; i < Enemy.All.Count; i++)
                {
                    Enemy e = Enemy.All[i];
                    if (!e.IsAlive || e.Group != group) continue;
                    alive++;
                    float d = (e.transform.position - player.transform.position).sqrMagnitude;
                    if (d < best)
                    {
                        best = d;
                        nearest = e;
                    }
                }
                if (alive == 0) yield break;
                Objective(title, string.Format(format, alive), nearest.AimPoint);
                yield return tick;
            }
        }

        IEnumerator Complete(string title, int xp, int credits)
        {
            hud.Banner("ЗАДАНИЕ ВЫПОЛНЕНО", title + "  ·  +" + xp + " ОПЫТА  ·  +" + credits + " ¢", 4.5f);
            Sound.Play(Sfx.Objective, 0.8f);
            Progress.AddXp(xp);
            Progress.AddCredits(credits);
            Objective("", "", null);
            yield return new WaitForSeconds(4f);
        }

        // ------------------------------------------------------------------ act one

        IEnumerator ActOne()
        {
            Act = 1;
            Vector3 start = city.Poi("start");
            Vector3 vendor = city.Poi("vendor");
            SetCheckpoint(start, 90f);

            hud.Banner("МЕРИДИАН", "СЕКТОР 7  ·  НИЖНИЙ ЯРУС  ·  02:14", 5f);
            yield return new WaitForSeconds(1.5f);
            Say("Очнулся? Хорошо. Имплант связи работает — значит, «Гелиос» тебя ещё не вычислил.");
            Say("Слушай. В Шпиле корпорации лежит «Ключ Меридиана» — мастер-доступ ко всему городу. Мы его заберём.");
            Say("Но сначала снаряжение. Иди на Барахолку, к терминалу Техно-Рынка. Путь я отметила.");
            Objective("ПРОБУЖДЕНИЕ", "Доберись до терминала на Барахолке", vendor);

            float hintAt = Time.time + 9f;
            bool hintA = false, hintB = false;
            while (!Near(vendor, 62f))
            {
                if (!hintA && Time.time > hintAt)
                {
                    hintA = true;
                    Hud.Toast("SHIFT — бег   ·   ПРОБЕЛ — прыжок   ·   Q — рывок", UiKit.Dim);
                }
                if (!hintB && Time.time > hintAt + 9f)
                {
                    hintB = true;
                    Hud.Toast("TAB — сканер: метит врагов и запускает эксплойты", UiKit.Dim);
                }
                yield return tick;
            }

            // two marauders between the player and the market
            Vector3 ambush = Navigation.Snap(Vector3.Lerp(player.transform.position, vendor, 0.55f), 8f);
            Spawn(EnemyKind.Grunt, ambush, 3f, "ambush");
            Spawn(EnemyKind.Grunt, ambush, 3f, "ambush");
            hud.ClearDialogue();
            Say("Стой. Двое «Ржавых» между тобой и рынком. У тебя только «Оса» — целься в голову.");
            yield return GroupDead("ambush", "ПРОБУЖДЕНИЕ", "Устрани мародёров: {0}");

            Say("Чисто. Подбери, что с них выпало, и иди к терминалу.");
            Objective("ПРОБУЖДЕНИЕ", "Забери ПП «Шершень» в терминале Техно-Рынка", vendor);
            SetCheckpoint(vendor + new Vector3(0f, 0f, -5f), 0f);
            while (!weapons.slots[WeaponSystem.Smg].owned) yield return tick;

            Say("«Шершень» твой. Остальное — за кредиты, я не благотворительный фонд.");
            yield return Complete("ПРОБУЖДЕНИЕ", 120, 90);
        }

        // ------------------------------------------------------------------ act two

        IEnumerator ActTwo()
        {
            Act = 2;
            Vector3 gate = city.Poi("hideoutGate"), core = city.Poi("hideoutCore");
            hud.ClearDialogue();
            Say("Плохая новость. «Ржавые» утащили мой ретранслятор — без него Шпиль не открыть.");
            Say("Их логово в Ржавом Дворе, на севере. Забери шард ретранслятора. По-хорошему не отдадут.");
            Objective("РЖАВАЯ СТАЯ", "Доберись до логова «Ржавых»", gate);
            while (!Near(gate, 62f)) yield return tick;

            SpawnHideout(core);
            SetCheckpoint(gate + new Vector3(0f, 0f, -7f), 0f);
            Say("Вижу десяток сигнатур. У них там бочки с топливом: TAB и 4 — подрыв издалека.");
            yield return GroupDead("hideout", "РЖАВАЯ СТАЯ", "Зачисти логово: осталось {0}");

            bool taken = false;
            Pickup.Spawn(PickupKind.Shard, core, 1, true, () => taken = true);
            Objective("РЖАВАЯ СТАЯ", "Забери шард ретранслятора", core);
            Say("Логово чистое. Шард где-то в центре двора — ищи маяк.");
            while (!taken) yield return tick;

            if (!weapons.slots[WeaponSystem.Shotgun].owned)
            {
                weapons.Give(WeaponSystem.Shotgun, 24);
                Hud.Toast("ПОЛУЧЕНО: ДРОБОВИК «ГРОМ-12» [3]", UiKit.Amber);
            }
            Say("Шард у тебя. И прихвати их дробовик — «Гром» им больше не нужен.");
            yield return Complete("РЖАВАЯ СТАЯ", 220, 160);
        }

        void SpawnHideout(Vector3 core)
        {
            Vector3[] posts =
            {
                core + new Vector3(-12f, 0f, -8f), core + new Vector3(10f, 0f, -10f), core + new Vector3(-16f, 0f, 6f),
                core + new Vector3(15f, 0f, 8f), core + new Vector3(0f, 0f, 12f), core + new Vector3(3f, 0f, -14f)
            };
            for (int i = 0; i < posts.Length; i++)
            {
                Enemy e = EnemyFactory.Spawn(EnemyKind.Grunt, posts[i], Random.Range(0f, 360f), "hideout");
                List<Vector3> route = new List<Vector3> { posts[i], posts[(i + 2) % posts.Length], posts[(i + 4) % posts.Length] };
                e.SetPatrol(route);
            }
            EnemyFactory.Spawn(EnemyKind.Rusher, core + new Vector3(-6f, 0f, 14f), 180f, "hideout");
            EnemyFactory.Spawn(EnemyKind.Rusher, core + new Vector3(8f, 0f, 15f), 180f, "hideout");
            EnemyFactory.Spawn(EnemyKind.Rusher, core + new Vector3(18f, 0f, -2f), 200f, "hideout");
            EnemyFactory.Spawn(EnemyKind.Heavy, core + new Vector3(0f, 0f, 4f), 180f, "hideout");
        }

        // ------------------------------------------------------------------ act three

        IEnumerator ActThree()
        {
            Act = 3;
            hud.ClearDialogue();
            Say("Теперь мачты связи «Гелиоса». Их три. Перенастроим — и Шпиль ослепнет на пару минут.");
            Say("У каждой мачты консоль. Взлом займёт несколько секунд, а охрана ждать не станет.");

            string[] keys = { "relayA", "relayB", "relayC" };
            Vector3[] consoles = new Vector3[3];
            bool[] done = new bool[3], guarded = new bool[3];
            Interactable[] points = new Interactable[3];
            int count = 0;

            for (int i = 0; i < 3; i++)
            {
                int index = i;
                consoles[i] = city.Poi(keys[i] + "_console");
                points[i] = Interactable.Create(consoles[i], "ВЗЛОМАТЬ РЕТРАНСЛЯТОР", 4f, () =>
                {
                    if (done[index]) return;
                    done[index] = true;
                    count++;
                    Destroy(points[index].gameObject);
                    Sound.Play(Sfx.HackOk, 0.8f);
                    Sound.Play(Sfx.Objective, 0.6f);
                    Hud.Toast("РЕТРАНСЛЯТОР ПЕРЕНАСТРОЕН  " + count + " / 3", UiKit.Cyan);
                    SetCheckpoint(consoles[index] + new Vector3(0f, 0f, -3f), 0f);
                    // the corporation notices
                    if (count < 3)
                    {
                        SpawnDrone(consoles[index] + new Vector3(0f, 0f, -14f), 10f, "relay" + index).Aggro(player.transform.position);
                        SpawnDrone(consoles[index] + new Vector3(0f, 0f, 14f), 10f, "relay" + index).Aggro(player.transform.position);
                    }
                }, UiKit.Cyan);
            }

            while (count < 3)
            {
                int nearest = -1;
                float best = float.MaxValue;
                for (int i = 0; i < 3; i++)
                {
                    if (done[i]) continue;
                    float d = (consoles[i] - player.transform.position).sqrMagnitude;
                    if (d < best)
                    {
                        best = d;
                        nearest = i;
                    }
                    if (!guarded[i] && d < 58f * 58f)
                    {
                        guarded[i] = true;
                        SpawnRelayGuards(i, consoles[i]);
                    }
                }
                if (nearest >= 0) Objective("ТРИ ИГЛЫ", "Взломай ретрансляторы «Гелиоса»: " + count + " / 3", consoles[nearest]);
                yield return tick;
            }

            if (!weapons.slots[WeaponSystem.Rail].owned)
            {
                weapons.Give(WeaponSystem.Rail, 12);
                Hud.Toast("ПОЛУЧЕНО: РЕЛЬСОТРОН «ИГЛА» [4]", UiKit.Amber);
            }
            Say("Третья готова. В тайнике у мачты был рельсотрон «Игла» — он твой. Пробивает насквозь.");
            yield return Complete("ТРИ ИГЛЫ", 320, 260);
        }

        void SpawnRelayGuards(int index, Vector3 console)
        {
            string group = "relay" + index;
            Vector3 mast = console + new Vector3(0f, 0f, 4f);
            Spawn(EnemyKind.Trooper, mast + new Vector3(-9f, 0f, -8f), 2f, group);
            Spawn(EnemyKind.Trooper, mast + new Vector3(9f, 0f, -8f), 2f, group);
            Spawn(EnemyKind.Trooper, mast + new Vector3(0f, 0f, 12f), 2f, group);
            SpawnDrone(mast, 9f, group);
            if (index >= 1) SpawnDrone(mast, 9f, group);
            if (index == 1) EnemyFactory.Spawn(EnemyKind.Turret, Navigation.Snap(mast + new Vector3(8f, 0f, 6f)), 180f, group);
            if (index == 2) Spawn(EnemyKind.Heavy, mast + new Vector3(-6f, 0f, 9f), 2f, group);
            if (index == 0) Say("Охрана «Гелиоса». Легионеры бьют точнее мародёров — не стой на месте.");
        }

        // ------------------------------------------------------------------ act four

        IEnumerator ActFour()
        {
            Act = 4;
            Vector3 tower = city.Poi("tower"), console = city.Poi("towerGate"), arena = city.Poi("bossSpawn"), exit = city.Poi("extraction");
            hud.ClearDialogue();
            Say("Шпиль ослеп. У тебя окно. Иди к главному входу и вскрой терминал шлюза.");
            Objective("ШПИЛЬ", "Доберись до Шпиля «Гелиос»", tower);
            while (!Near(tower, 78f)) yield return tick;

            for (int i = 0; i < 4; i++)
            {
                Vector3 pad = city.Poi("turret" + i);
                if (pad != Vector3.zero) EnemyFactory.Spawn(EnemyKind.Turret, pad, 180f, "tower");
            }
            Spawn(EnemyKind.Trooper, tower + new Vector3(-14f, 0f, 2f), 2f, "tower");
            Spawn(EnemyKind.Trooper, tower + new Vector3(14f, 0f, 2f), 2f, "tower");
            Spawn(EnemyKind.Trooper, tower + new Vector3(-6f, 0f, 8f), 2f, "tower");
            Spawn(EnemyKind.Trooper, tower + new Vector3(7f, 0f, 8f), 2f, "tower");
            Spawn(EnemyKind.Heavy, tower + new Vector3(0f, 0f, 6f), 2f, "tower");
            SetCheckpoint(tower + new Vector3(0f, 0f, -22f), 0f);
            Say("Турели на площади. Перехвати их — TAB и 3 — пусть поработают на нас.");

            bool open = false;
            Interactable gate = Interactable.Create(console, "ВСКРЫТЬ ШЛЮЗ", 5f, () => open = true, UiKit.Amber);
            Objective("ШПИЛЬ", "Вскрой терминал шлюза", console);
            while (!open) yield return tick;
            Destroy(gate.gameObject);

            // the walker
            Sound.Play(Sfx.Thunder, 0.8f);
            ScreenShake.Add(0.6f);
            hud.ClearDialogue();
            hud.Banner("ЦЕРБЕР", "ТЯЖЁЛАЯ ПЛАТФОРМА ОХРАНЫ", 4f);
            Enemy boss = EnemyFactory.Spawn(EnemyKind.Boss, arena, 180f, "boss");
            boss.Aggro(player.transform.position);
            hud.ShowBoss(boss);
            Say("Чёрт. Они выпустили «Цербера». Бей в вентиляцию на спине — и прыгай, когда он топает!");
            SetCheckpoint(tower + new Vector3(0f, 0f, -24f), 0f);
            while (boss != null && boss.IsAlive)
            {
                Objective("ШПИЛЬ", "Уничтожь «Цербера»", boss.AimPoint);
                yield return tick;
            }

            bool key = false;
            Pickup.Spawn(PickupKind.Shard, arena, 1, true, () => key = true);
            Say("Он готов. Ключ — в обломках. Забирай!");
            Objective("ШПИЛЬ", "Забери «Ключ Меридиана»", arena);
            while (!key) yield return tick;

            Say("Ключ у тебя. Уходи на юг — челнок заберёт тебя на перекрёстке у кольцевой.");
            Objective("ШПИЛЬ", "Доберись до точки эвакуации", exit);
            GameObject beacon = WorldFx.Beacon(exit, new Color(0.3f, 1f, 0.6f));
            yield return new WaitForSeconds(4f);
            for (int i = 0; i < 3; i++) SpawnDrone(player.transform.position + player.transform.forward * -20f, 10f, "chase").Aggro(player.transform.position);
            Spawn(EnemyKind.Trooper, Vector3.Lerp(player.transform.position, exit, 0.5f), 5f, "chase").Aggro(player.transform.position);
            Spawn(EnemyKind.Trooper, Vector3.Lerp(player.transform.position, exit, 0.6f), 5f, "chase").Aggro(player.transform.position);
            while (!Near(exit, 9f)) yield return tick;

            Destroy(beacon);
            yield return Complete("ШПИЛЬ", 500, 500);
            Finished = true;
            game.ShowEnding();
        }
    }
}
