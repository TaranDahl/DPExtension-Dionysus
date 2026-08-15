using DynamicPatcher;
using Extension.Ext;
using Extension.Script;
using InteropUtils;
using PatcherYRpp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using COLORREF = System.UInt32;

namespace Scripts
{
    [Serializable]
    public class BoomBot : TechnoScriptable
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

        public BoomBot(TechnoExt owner) : base(owner)
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

        public override void OnUpdate()
        {
            var pThis = Owner.OwnerObject;
            if (pThis.IsNull)
                return;

            UpdateInputCounters();
        }

        /// <summary>
        /// 输入数字（预留给输入钩子调用）
        /// </summary>
        /// <param name="digit">输入数字 0~9</param>
        /// <param name="inputHouseArrayIndex">输入方 HouseClass.ArrayIndex</param>
        public int SubmitInputDigit(int digit, int inputHouseArrayIndex)
        {
            // 加日志便于调试输入流程
            Logger.Log("BoomBot.SubmitInputDigit called: digit={0}, inputHouseArrayIndex={1}, isDisarmed={2}, inputLockCounter={3}, inputOccupationCounter={4}",
                digit, inputHouseArrayIndex, isDisarmed, inputLockCounter, inputOccupationCounter);

            // 参数校验
            if (digit < 0 || digit > 9)
            {
                Logger.Log("BoomBot.SubmitInputDigit -> InvalidDigit");
                return (int)InputProcessResult.InvalidDigit;
            }

            if (isDisarmed)
            {
                Logger.Log("BoomBot.SubmitInputDigit -> AlreadyDisarmed");
                return (int)InputProcessResult.AlreadyDisarmed;
            }

            // 被其它作战方占用
            if (IsOccupiedByOtherHouse(inputHouseArrayIndex))
            {
                // 仅在本机玩家输入时显示提示，且提示颜色与 Player 相同
                if (HouseClass.Player.IsNotNull && inputHouseArrayIndex == HouseClass.Player.Ref.ArrayIndex)
                {
                    MessageListClass.Instance.PrintMessage("你的盟友已经在输入密码。", (ColorSchemeIndex)HouseClass.Player.Ref.ColorSchemeIndex);
                }
                Logger.Log("BoomBot.SubmitInputDigit -> IgnoredByOtherHouseOccupation");
                return (int)InputProcessResult.IgnoredByOtherHouseOccupation;
            }

            // 输入锁（错误后8秒内）
            if (inputLockCounter > 0)
            {
                if (HouseClass.Player.IsNotNull && inputHouseArrayIndex == HouseClass.Player.Ref.ArrayIndex)
                {
                    MessageListClass.Instance.PrintMessage("请稍后再试！", (ColorSchemeIndex)HouseClass.Player.Ref.ColorSchemeIndex);
                }
                Logger.Log("BoomBot.SubmitInputDigit -> Locked (inputLockCounter={0})", inputLockCounter);
                return (int)InputProcessResult.Locked;
            }

            var pThis = Owner.OwnerObject;
            if (pThis.IsNull)
            {
                Logger.Log("BoomBot.SubmitInputDigit -> AlreadyDisarmed (pThis null)");
                return (int)InputProcessResult.AlreadyDisarmed;
            }

            // 本次输入被接收，刷新占用
            OccupyByHouse(inputHouseArrayIndex);

            var expected = codeDigits[currentIndex];
            Logger.Log("BoomBot.SubmitInputDigit expected digit at index {0} = {1}", currentIndex, expected);
            if (digit == expected)
            {
                hasWrongInputAtCurrent = false;
                currentIndex++;

                if (currentIndex >= CodeLength)
                {
                    isDisarmed = true;
                    pThis.Ref.Base.KillSelfByDamage(false);
                    Logger.Log("BoomBot disarmed (correct code entered).");
                }
                else
                {
                    Logger.Log("BoomBot correct digit, advanced to index {0}", currentIndex);
                }

                return (int)InputProcessResult.AcceptedCorrect;
            }

            // 错误：标红并触发惩罚、输入锁
            hasWrongInputAtCurrent = true;
            ApplyWrongInputPunishment(pThis);
            StartInputLock(pThis);

            // 玩家输入时提示错误，提示颜色与 Player 相同
            if (HouseClass.Player.IsNotNull && inputHouseArrayIndex == HouseClass.Player.Ref.ArrayIndex)
            {
                MessageListClass.Instance.PrintMessage("密码错误！", (ColorSchemeIndex)HouseClass.Player.Ref.ColorSchemeIndex);
            }

            Logger.Log("BoomBot.SubmitInputDigit -> AcceptedWrong (got {0}, expected {1})", digit, expected);
            return (int)InputProcessResult.AcceptedWrong;
        }

        /// <summary>
        /// 预留给“单位头顶数字显示”钩子调用。
        /// 这里只绘制数字到指定屏幕点（炸弹机器人不会是建筑/步兵/带盾），因此不再需要 isBuilding/isInfantry/hasShield 参数。
        /// 已加入详细日志以调试为什么数字无法显示。
        /// </summary>
        public void DrawDigitsOnTop()
        {
            var pThis = Owner.OwnerObject;
            if (pThis.IsNull)
            {
                Logger.Log("BoomBot.DrawDigitsOnTop aborted: Owner.OwnerObject is null");
                return;
            }

            try
            {
                // 获取世界坐标
                var worldCrd = pThis.Ref.BaseAbstract.GetCoords();
                Logger.Log("BoomBot.DrawDigitsOnTop worldCrd = ({0}, {1}, {2})", worldCrd.X, worldCrd.Y, worldCrd.Z);

                // 将世界坐标转换为屏幕/客户端坐标
                Point2D screenPos;
                try
                {
                    screenPos = TacticalClass.Instance.Ref.CoordsToClient(worldCrd);
                    Logger.Log("BoomBot.DrawDigitsOnTop CoordsToClient -> screenPos = ({0}, {1})", screenPos.X, screenPos.Y);
                }
                catch (Exception exCoord)
                {
                    Logger.Log("BoomBot.DrawDigitsOnTop CoordsToClient threw: {0}", exCoord);
                    return;
                }

                var digits = GetDisplayDigits();
                if (digits == null || digits.Count == 0)
                {
                    Logger.Log("BoomBot.DrawDigitsOnTop no digits to draw (null or empty).");
                    return;
                }

                // 输出要绘制的数字和颜色，便于验证数据是否正确
                var sb = new StringBuilder();
                sb.Append("BoomBot.DrawDigitsOnTop digits:");
                for (int i = 0; i < digits.Count; i++)
                {
                    var (digit, colorStruct) = digits[i];
                    sb.AppendFormat(" [{0} (R{1} G{2} B{3})]", digit, colorStruct.R, colorStruct.G, colorStruct.B);
                }
                Logger.Log(sb.ToString());

                const int textHeight = 12;
                screenPos.Y -= textHeight; // 顶部偏移

                var rect = DSurface.Composite.Ref.Base.Base.GetRect();
                Logger.Log("BoomBot.DrawDigitsOnTop rect = X:{0} Y:{1} W:{2} H:{3}", rect.X, rect.Y, rect.Width, rect.Height);
                rect.Height -= 32;

                var printType = TextPrintType.Point6 | TextPrintType.FullShadow;

                const int digitSpacing = 8;

                // 在绘制前检查 DSurface.Composite 是否可用
                try
                {
                    var comp = DSurface.Composite;
                    Logger.Log("BoomBot.DrawDigitsOnTop DSurface.Composite pointer ok.");
                }
                catch (Exception exComp)
                {
                    Logger.Log("BoomBot.DrawDigitsOnTop DSurface.Composite access threw: {0}", exComp);
                }

                try
                {
                    for (int i = 0; i < digits.Count; i++)
                    {
                        var (digit, colorStruct) = digits[i];
                        // 将 ColorStruct 转为 COLORREF（仓库中 Win32Color.RGB 可用）
                        COLORREF color = Win32Color.RGB((byte)colorStruct.R, (byte)colorStruct.G, (byte)colorStruct.B);
                        string digitText = digit.ToString();
                        var drawPos = new Point2D(screenPos.X + i * digitSpacing, screenPos.Y);
                        DSurface.Composite.Ref.DrawText(digitText, ref rect, ref drawPos, color, 0, printType);
                        Logger.Log("BoomBot.DrawDigitsOnTop DrawText: '{0}' at ({1},{2}) color={3:X8}", digitText, drawPos.X, drawPos.Y, color);
                    }
                }
                catch (Exception exDraw)
                {
                    Logger.Log("BoomBot.DrawDigitsOnTop DrawText threw: {0}", exDraw);
                }
            }
            catch (Exception e)
            {
                Logger.Log("BoomBot.DrawDigitsOnTop top-level exception: {0}", e);
            }
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

            // 日志：输出当前 codeDigits 和索引，帮助确认显示逻辑
            try
            {
                var s = string.Join(",", result.Select((t, idx) => string.Format("{0}:{1}/{2},{3}", idx, t.digit, t.color.R, t.color.G)));
                Logger.Log("BoomBot.GetDisplayDigits currentIndex={0}, hasWrong={1}, data={2}", currentIndex, hasWrongInputAtCurrent, s);
            }
            catch { /* 忽略日志失败 */ }

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
                PhobosAttachEffect.Detach(pThis, new[] { GetAccelAeName(speedPenaltyLevel) });
            }

            speedPenaltyLevel++;

            PhobosAttachEffect.Attach(
                pThis,
                pThis.Ref.BaseAbstract.GetOwningHouse(),
                pThis,
                Pointer<AbstractClass>.Zero,
                new[] { GetAccelAeName(speedPenaltyLevel) });
        }

        private static string GetAccelAeName(int level)
        {
            return "MutBoomBotAccelAE" + level;
        }
    }
}
