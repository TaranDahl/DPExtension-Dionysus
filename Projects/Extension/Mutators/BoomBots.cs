using DynamicPatcher;
using Extension.Decorators;
using Extension.Ext;
using Extension.Script;
using Extension.Utilities;
using InteropUtils;
using PatcherYRpp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using COLORREF = System.UInt32;

namespace Extension.Mutators
{
    [Serializable]
    public class BoomBots : TargetTechnoMutator
    {
        private static Pointer<UnitTypeClass> bot => new Pointer<UnitTypeClass>(UnitTypeClass.ABSTRACTTYPE_ARRAY.Find("MUTBOOMBOT"));
        private static string SpawnSound => "EVA_UnitsInCombat";

        public override string UIName => "炸弹机器人";
        public override string Description => "对一切都毫不在意的机器人携带着聚变弹头朝你的基地进发。一名玩家必须识别出拆弹的顺序，另一名玩家则必须正确输入才能解除危机。";
        public override bool IsAvailableInRPG => false;
        public override int Score => 4;

        private int counter = 0;

        public BoomBots(Pointer<HouseClass> owner) : base(owner) { }

        public override bool Update()
        {
            if (!base.Update())
                return false;

            // 2分28秒开始生效
            if (Game.CurrentFrame >= TimeToFrame(2, 28))
            {
                if (counter == 0)
                {
                    List<Pointer<BuildingClass>> availableBuildings = new List<Pointer<BuildingClass>>();
                    foreach (var house in HouseClass.Array)
                    {
                        if (!IsOnOurSide(house))
                            continue;

                        foreach (var building in house.Ref.Buildings)
                        {
                            if (IsTechnoValid(building.Convert<TechnoClass>()))
                                availableBuildings.Add(building);
                        }
                    }
                    if (availableBuildings.Count > 0)
                    {
                        VoxClass.Play(SpawnSound);

                        foreach (var type in GetTypesToSpawn())
                        {
                            var spawner = ScenarioClass.GetRandomInList(availableBuildings);
                            var spawnCrd = spawner.Ref.Base.BaseAbstract.GetCoords();
                            var techno = bot.Ref.BaseObjectType.CreateObject(spawner.Ref.Base.BaseAbstract.GetOwningHouse()).Convert<TechnoClass>();
                            ++Game.IKnowWhatImDoing;
                            var putOk = techno.Ref.Base.Put(spawnCrd, ScenarioClass.GetRandomInEnum<DirType>());
                            --Game.IKnowWhatImDoing;

                            if (!putOk)
                            {
                                techno.Ref.BaseAbstract.DTOR();
                            }
                            else
                            {
                                // 给该单位添加装饰器（唯一的场景 id）
                                var ext = TechnoExt.ExtMap.Find(techno);
                                ext.CreateDecorator<BoomBotScript>(BoomBotScript.ID, "BoomBotDecorator");
                            }
                        }
                    }
                }

                counter++;
                counter %= GetSpawnDelay();
            }
            return true;
        }

        protected override unsafe bool IsTechnoValid(Pointer<TechnoClass> techno)
        {
            // 必须是建筑
            if (techno.Ref.Base.Base.WhatAmI() != AbstractType.Building)
                return false;

            // 必须是友军（突变因子拥有者视角，实际上是玩家的敌人）
            var technoOwner = techno.Ref.Base.Base.GetOwningHouse();
            if (!IsOnOurSide(technoOwner))
                return false;

            // 必须在地图内
            var technoCell = techno.Ref.Base.GetCell();
            if (!MapClass.Instance.IsWithinUsableArea(ref technoCell.Ref.MapCoords, true))
                return false;

            // 必须不在水里
            var landType = technoCell.Ref.LandType;
            if (landType == LandType.Water || landType == LandType.Beach)
                return false;

            // 必须和至少一个敌人基地位置陆地联通
            foreach (var house in HouseClass.Array)
            {
                // 目标必须是敌人
                if (!IsOnTheirSide(house))
                    continue;

                foreach (var building in house.Ref.Buildings)
                {
                    // 目标必须不在水里
                    var buildingCell = building.Ref.Base.Base.GetCell();
                    var targetLandType = buildingCell.Ref.LandType;
                    if (targetLandType == LandType.Water || targetLandType == LandType.Beach)
                        continue;

                    // 目标必须是保持存活的建筑
                    if (GetKeepAliveAbility(building.Convert<TechnoClass>()) == 0)
                        continue;

                    // 目标必须联通
                    if (MapClass.Instance.IsInSameZone(ref technoCell.Ref.MapCoords, ref buildingCell.Ref.MapCoords, MovementZone.Infantry, false, false, false))
                        return true;
                }
            }

            return false;
        }

        private List<Pointer<TechnoTypeClass>> GetTypesToSpawn()
        {
            var list = new List<Pointer<TechnoTypeClass>>();
            var type = bot.Convert<TechnoTypeClass>();

            if (type.IsNull)
                return list;

            var frame = Game.CurrentFrame;

            // 2分28秒到12分前，每次生成1个
            if (frame < TimeToFrame(12, 0))
            {
                list.Add(type);
            }
            // 12分到18分前，每次生成2个
            else if (frame < TimeToFrame(18, 0))
            {
                list.Add(type);
                list.Add(type);
            }
            // 18分后，每次生成2个
            else
            {
                list.Add(type);
                list.Add(type);
            }

            return list;
        }

        private int GetSpawnDelay()
        {
            var frame = Game.CurrentFrame;

            // 12分前，每90秒生成一次
            if (frame < TimeToFrame(12, 0))
                return TimeToFrame(0, 90);

            // 12分到18分前，每70秒生成一次
            if (frame < TimeToFrame(18, 0))
                return TimeToFrame(0, 70);

            // 18分后，每50秒生成一次
            return TimeToFrame(0, 50);
        }


        [Serializable]
        public class BoomBotScript : EventDecorator
        {
            private enum InputProcessResult
            {
                AcceptedCorrect = 0,
                AcceptedWrong = 1,
                Locked = 2,
                InvalidDigit = 3,
                AlreadyDisarmed = 4,
                IgnoredByOtherHouseOccupation = 5
            }

            public static DecoratorId ID => new DecoratorId((int)TechnoDecoratorIDs.UniqueDecorator);
            private static Pointer<AnimTypeClass> CooldownAnimType => AnimTypeClass.ABSTRACTTYPE_ARRAY.Find("MutBoomBotCDAnim");
            private static ColorStruct CorrectColor => ColorStruct.Green;
            private static ColorStruct WrongColor => ColorStruct.Red;
            private static ColorStruct PendingColor => new ColorStruct(252, 252, 0);

            private const int CodeLength = 4;
            private static int InputLockFrames => ScenarioExt.TimeToFrame(0, 8);
            private static int InputOccupationFrames => ScenarioExt.TimeToFrame(0, 3);
            private const int MaxAccelAeLevel = 10;

            private int[] codeDigits = new int[CodeLength];
            private bool isDisarmed = false;

            private int currentIndex = 0;
            private bool hasWrongInputAtCurrent = false;

            private int inputLockCounter = 0;
            private int speedPenaltyLevel = 0;

            // 输入占用状态
            private int inputOccupationCounter = 0;
            private int inputOccupationHouseArrayIndex = -1;

            // 新增：寻敌周期计数（每次搜索后设为 3 秒对应的帧数）
            private int searchCooldownCounter = 0;

            public BoomBotScript() : base()
            {
                for (var i = 0; i < CodeLength; i++)
                {
                    codeDigits[i] = ScenarioClass.Instance.Random.RandomRanged(0, 9);
                }

                currentIndex = 0;
                hasWrongInputAtCurrent = false;
                inputLockCounter = 0;
                speedPenaltyLevel = 0;
                inputOccupationCounter = 0;
                inputOccupationHouseArrayIndex = -1;
                isDisarmed = false;
            }

            // Helper: 获取对应的 TechnoExt 与 Techno 指针
            private TechnoExt GetTechnoExt() => this.Decorative as TechnoExt;
            private Pointer<TechnoClass> GetTechno() => GetTechnoExt()?.OwnerObject ?? Pointer<TechnoClass>.Zero;

            // IEventDecorator.OnUpdate
            public override void OnUpdate()
            {
                UpdateInputCounters();

                var pThis = GetTechno();
                if (pThis.IsNull || !pThis.Ref.Base.IsAlive)
                    return;

                // 如果已经处于 AttackMove（MegaMission）就不做任何事
                var foot = pThis.Convert<FootClass>();
                if (foot.Ref.MegaMission == Mission.AttackMove)
                    return;

                // 寻敌周期限制：最多每 3 秒一次
                if (searchCooldownCounter > 0)
                {
                    searchCooldownCounter--;
                    return;
                }
                // 重置为 3 秒（按场景帧速）
                searchCooldownCounter = ScenarioExt.TimeToFrame(0, 3);

                // 全地图范围寻找 KeepAlive 等级最高的目标（若同等级则取最近）
                Pointer<TechnoClass> bestTech = Pointer<TechnoClass>.Zero;
                int bestLevel = -1;
                double bestDist = double.MaxValue;
                var myCrd = pThis.Ref.BaseAbstract.GetCoords();

                foreach (var house in HouseClass.Array)
                {
                    if (house.IsNull)
                        continue;

                    var candidate = Extension.AttackWave.AttackWave.GetMostImporantantTarget(house, myCrd, false);
                    if (candidate.techno.IsNull)
                        continue;

                    int lvl = (int)candidate.level;
                    if (lvl > bestLevel)
                    {
                        bestLevel = lvl;
                        bestTech = candidate.techno;
                        bestDist = candidate.techno.Ref.BaseAbstract.GetCoords().DistanceFrom(myCrd);
                    }
                    else if (lvl == bestLevel && candidate.techno.IsNotNull && bestTech.IsNotNull)
                    {
                        var d = candidate.techno.Ref.BaseAbstract.GetCoords().DistanceFrom(myCrd);
                        if (d < bestDist)
                        {
                            bestTech = candidate.techno;
                            bestDist = d;
                        }
                    }
                }

                if (bestTech.IsNotNull)
                {
                    // 发起 AttackMove
                    var footThis = pThis.Convert<FootClass>();
                    if (footThis.IsNotNull)
                    {
                        footThis.Ref.TryAttackMove(bestTech.Convert<AbstractClass>());
                    }
                }
            }

            public int SubmitInputDigit(int digit, int inputHouseArrayIndex)
            {
                // 参数校验
                if (digit < 0 || digit > 9)
                {
                    return (int)InputProcessResult.InvalidDigit;
                }

                if (isDisarmed)
                {
                    return (int)InputProcessResult.AlreadyDisarmed;
                }

                // 被其它作战方占用
                if (IsOccupiedByOtherHouse(inputHouseArrayIndex))
                {
                    if (HouseClass.Player.IsNotNull && inputHouseArrayIndex == HouseClass.Player.Ref.ArrayIndex)
                    {
                        MessageListClass.Instance.PrintMessage("你的盟友已经在输入密码。", (ColorSchemeIndex)HouseClass.Player.Ref.ColorSchemeIndex);
                    }
                    return (int)InputProcessResult.IgnoredByOtherHouseOccupation;
                }

                // 输入锁（错误后8秒内）
                if (inputLockCounter > 0)
                {
                    if (HouseClass.Player.IsNotNull && inputHouseArrayIndex == HouseClass.Player.Ref.ArrayIndex)
                    {
                        MessageListClass.Instance.PrintMessage("请稍后再试！", (ColorSchemeIndex)HouseClass.Player.Ref.ColorSchemeIndex);
                    }
                    return (int)InputProcessResult.Locked;
                }

                var pThis = (Decorative as TechnoExt).OwnerObject;
                if (pThis.IsNull)
                {
                    return (int)InputProcessResult.AlreadyDisarmed;
                }

                // 本次输入被接收，刷新占用
                OccupyByHouse(inputHouseArrayIndex);

                var expected = codeDigits[currentIndex];
                if (digit == expected)
                {
                    hasWrongInputAtCurrent = false;
                    currentIndex++;

                    if (currentIndex >= CodeLength)
                    {
                        isDisarmed = true;
                        pThis.Ref.Base.KillSelfByDamage(false);
                    }

                    return (int)InputProcessResult.AcceptedCorrect;
                }

                // 错误：标红并触发惩罚、输入锁
                hasWrongInputAtCurrent = true;
                ApplyWrongInputPunishment(pThis);
                StartInputLock(pThis);

                if (HouseClass.Player.IsNotNull && inputHouseArrayIndex == HouseClass.Player.Ref.ArrayIndex)
                {
                    MessageListClass.Instance.PrintMessage("密码错误！", (ColorSchemeIndex)HouseClass.Player.Ref.ColorSchemeIndex);
                }

                return (int)InputProcessResult.AcceptedWrong;
            }

            /// <summary>
            /// 预留给“单位头顶数字显示”钩子调用。
            /// 这里只绘制数字到指定屏幕点（炸弹机器人不会是建筑/步兵/带盾），因此不再需要 isBuilding/isInfantry/hasShield 参数。
            /// </summary>
            public void DrawDigitsOnTop()
            {
                var pThis = (Decorative as TechnoExt).OwnerObject;
                if (pThis.IsNull)
                    return;

                try
                {
                    // 获取世界坐标
                    var worldCrd = pThis.Ref.BaseAbstract.GetCoords();

                    // 将世界坐标转换为屏幕/客户端坐标
                    Point2D screenPos = TacticalClass.Instance.Ref.CoordsToClient(worldCrd);
                    var digits = GetDisplayDigits();
                    if (digits == null || digits.Count == 0)
                        return;

                    const int textHeight = 12;
                    screenPos.Y -= textHeight; // 顶部偏移
                    screenPos.X -= (digits.Count - 1) * 4; // 水平居中调整（假设每个数字宽度约为 8 像素，间距为 8 像素）

                    var rect = DSurface.Composite.Ref.Base.Base.GetRect();
                    rect.Height -= 32;

                    var printType = TextPrintType.Point6 | TextPrintType.FullShadow;

                    const int digitSpacing = 8;

                    for (int i = 0; i < digits.Count; i++)
                    {
                        var (digit, colorStruct) = digits[i];
                        COLORREF color = Win32Color.RGB((byte)colorStruct.R, (byte)colorStruct.G, (byte)colorStruct.B);
                        string digitText = digit.ToString();
                        var drawPos = new Point2D(screenPos.X + i * digitSpacing, screenPos.Y);
                        DSurface.Composite.Ref.DrawText(digitText, ref rect, ref drawPos, color, 0, printType);
                    }
                }
                catch { }
            }

            /// <summary>
            /// 返回当前显示用的4位数字及颜色状态（绿=正确，红=错误待更正，黄=未输入）。
            /// </summary>
            public List<(int digit, ColorStruct color)> GetDisplayDigits()
            {
                var result = new List<(int digit, ColorStruct color)>(CodeLength);
                for (var i = 0; i < CodeLength; i++)
                {
                    if (i < currentIndex)
                    {
                        result.Add((codeDigits[i], CorrectColor));
                    }
                    else if (i == currentIndex && hasWrongInputAtCurrent)
                    {
                        result.Add((codeDigits[i], WrongColor));
                    }
                    else
                    {
                        result.Add((codeDigits[i], PendingColor));
                    }
                }

                return result;
            }

            private void StartInputLock(Pointer<TechnoClass> pThis)
            {
                inputLockCounter = InputLockFrames;
                CreateCooldownAnim(pThis);
            }

            private void UpdateInputCounters()
            {
                if (inputLockCounter > 0)
                {
                    inputLockCounter--;
                }

                if (inputOccupationCounter > 0)
                {
                    if (inputOccupationHouseArrayIndex == HouseClass.Player.Ref.ArrayIndex && inputOccupationCounter % 5 == 0)
                        MapClass.Instance.RevealArea1(GetTechno().Ref.BaseAbstract.GetCoords(), 2, HouseClass.Player, false, false, false, true, 0);

                    inputOccupationCounter--;
                    if (inputOccupationCounter == 0)
                    {
                        inputOccupationHouseArrayIndex = -1;
                    }
                }
            }

            private bool IsOccupiedByOtherHouse(int inputHouseArrayIndex)
            {
                return inputOccupationCounter > 0
                    && inputOccupationHouseArrayIndex >= 0
                    && inputHouseArrayIndex != inputOccupationHouseArrayIndex;
            }

            private void OccupyByHouse(int inputHouseArrayIndex)
            {
                inputOccupationHouseArrayIndex = inputHouseArrayIndex;
                inputOccupationCounter = InputOccupationFrames;
            }

            private void CreateCooldownAnim(Pointer<TechnoClass> pThis)
            {
                if (CooldownAnimType.IsNull)
                    return;

                var anim = YRMemory.Create<AnimClass>(CooldownAnimType, pThis.Ref.BaseAbstract.GetCoords());
                anim.Ref.SetOwnerObject(pThis.Convert<ObjectClass>());
                anim.Ref.Owner = pThis.Ref.BaseAbstract.GetOwningHouse();
            }

            private void ApplyWrongInputPunishment(Pointer<TechnoClass> pThis)
            {
                if (pThis.IsNull)
                    return;

                // 已到最高等级，不再重复Detach/Attach
                if (speedPenaltyLevel >= MaxAccelAeLevel)
                    return;

                if (speedPenaltyLevel > 0)
                {
                    PhobosAttachEffect.Detach(pThis, new[] { GetAccelAeName(speedPenaltyLevel) }, out _);
                }

                speedPenaltyLevel++;

                PhobosAttachEffect.Attach(
                    pThis,
                    pThis.Ref.BaseAbstract.GetOwningHouse(),
                    pThis,
                    Pointer<AbstractClass>.Zero,
                    new[] { GetAccelAeName(speedPenaltyLevel) },
                    out _);
            }

            private static string GetAccelAeName(int level)
            {
                return "MutBoomBotAccelAE" + level;
            }
        }
        // ---------------------- 针对 hooks 的桥接函数（供 hook 调用） -------------------------

        public static unsafe UInt32 KeyboardProcessBridge(REGISTERS* R)
        {
            var cur = ObjectClass.CurrentObjects;
            if (cur.Count != 1)
                return 0;

            var pObj = cur[0];
            if (pObj.IsNull)
                return 0;

            // 读取按键（ECX 指向 KeyNumType）
            var pKey = (Pointer<KeyNumType>)R->ECX;
            if (pKey.IsNull)
                return 0;

            KeyNumType key = pKey.Ref;
            int ik = (int)key;

            // 支持主键盘 0..9 (0x30..0x39) 与小键盘 0..9 (VK_NUMPAD0..VK_NUMPAD9 => 0x60..0x69)
            int digit;
            if (ik >= 0x30 && ik <= 0x39) // KN_0..KN_9
            {
                digit = ik - 0x30;
            }
            else if (ik >= 0x60 && ik <= 0x69) // VK_NUMPAD0..VK_NUMPAD9
            {
                digit = ik - 0x60;
            }
            else
            {
                return 0;
            }

            // 获取 Techno 实例并找到扩展脚本
            var pTechno = pObj.Convert<TechnoClass>();
            var ext = TechnoExt.ExtMap.Find(pTechno);
            var boom = ext.Get(BoomBotScript.ID) as BoomBotScript;
            if (boom == null)
                return 0;

            int inputHouseArrayIndex = -1;
            if (HouseClass.Player.IsNotNull)
                inputHouseArrayIndex = HouseClass.Player.Ref.ArrayIndex;

            boom.SubmitInputDigit(digit, inputHouseArrayIndex);

            return 0x55E15B;
        }

        public static unsafe UInt32 DrawHealthBarBridge(REGISTERS* R)
        {
            var pTechno = (Pointer<TechnoClass>)R->ECX;
            if (pTechno.IsNull)
                return 0;

            var ext = TechnoExt.ExtMap.Find(pTechno);
            var boom = ext.Get(BoomBotScript.ID) as BoomBotScript;
            if (boom == null)
                return 0;

            boom.DrawDigitsOnTop();

            return 0;
        }
        // -------------------------------------------------------------------------
    }
}
