using DynamicPatcher;
using Extension.Mutators;
using InteropUtils;
using PatcherYRpp;
using System;
using System.Runtime.InteropServices;

namespace Extension.Ext
{
    public enum EventTypeExt : byte
    {
        MicroTransactions = 0xC0,
        BoomBotSubmit = 0xC1,

        FIRST = MicroTransactions,
        LAST = BoomBotSubmit
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1, Size = 9)]
    public struct BoomBotSubmitEventData
    {
        public TargetClass Who;
        public int InputDigit;
    }

    [StructLayout(LayoutKind.Explicit, Size = 111, Pack = 1)]
    public struct EventExt
    {
        [FieldOffset(0)] public EventTypeExt Type;
        [FieldOffset(1)] public Bool IsExecuted;
        [FieldOffset(2)] public sbyte HouseIndex;
        [FieldOffset(3)] public uint Frame;

        // 对齐 C++ EventClass::DataBuffer[104]
        [FieldOffset(7)] public unsafe fixed byte Payload[104];

        [FieldOffset(7)] public BoomBotSubmitEventData BoomBotSubmit;

        public bool AddEvent()
        {
            return PhobosEventExt.AddEvent(Pointer<EventExt>.AsPointer(ref this)) == HResult.S_OK;
        }

        public void RespondEvent()
        {
            switch (Type)
            {
                case EventTypeExt.MicroTransactions:
                    RespondToMicroTransactions();
                    break;
                case EventTypeExt.BoomBotSubmit:
                    RespondToBoomBotSubmit();
                    break;
            }
        }

        public static unsafe void RaiseMicroTransactions(Pointer<HouseClass> pSenderHouse)
        {
            if (pSenderHouse.IsNull)
                return;

            EventExt evt = default;
            evt.Type = EventTypeExt.MicroTransactions;
            evt.HouseIndex = (sbyte)pSenderHouse.Ref.ArrayIndex;
            evt.Frame = (uint)Game.CurrentFrame;

            evt.AddEvent();
        }

        public static unsafe void RaiseBoomBotSubmit(Pointer<HouseClass> pSenderHouse, Pointer<TechnoClass> pBoomBot, int inputDigit)
        {
            if (pSenderHouse.IsNull || pBoomBot.IsNull)
                return;

            EventExt evt = default;
            evt.Type = EventTypeExt.BoomBotSubmit;
            evt.HouseIndex = (sbyte)pSenderHouse.Ref.ArrayIndex;
            evt.Frame = (uint)Game.CurrentFrame;
            evt.BoomBotSubmit.Who = new TargetClass(pBoomBot);
            evt.BoomBotSubmit.InputDigit = inputDigit;

            evt.AddEvent();
        }

        public static int GetDataSize(EventTypeExt type)
        {
            switch (type)
            {
                case EventTypeExt.MicroTransactions:
                    return 0;
                case EventTypeExt.BoomBotSubmit:
                    return Marshal.SizeOf<BoomBotSubmitEventData>();
            }

            return 0;
        }

        public static bool IsValidType(EventTypeExt type)
        {
            return type >= EventTypeExt.FIRST && type <= EventTypeExt.LAST;
        }

        private void RespondToMicroTransactions()
        {
            if (HouseIndex < 0 || HouseIndex >= HouseClass.Array.Count)
                return;

            var pHouse = HouseClass.Array[HouseIndex];
            if (pHouse.IsNull)
                return;

            pHouse.Ref.TakeMoney(25);
        }

        private void RespondToBoomBotSubmit()
        {
            if (HouseIndex < 0 || HouseIndex >= HouseClass.Array.Count)
                return;

            var pBoomBot = BoomBotSubmit.Who.AsTechno();
            if (pBoomBot.IsNull)
                return;

            var technoExt = TechnoExt.ExtMap.Find(pBoomBot);
            var boomBotScript = technoExt.Get(BoomBots.BoomBotScript.ID) as BoomBots.BoomBotScript;
            if (boomBotScript != null)
            {
                boomBotScript.SubmitInputDigit(BoomBotSubmit.InputDigit, HouseIndex);
            }
        }

        // ---------------- Hook handlers ----------------

        // 0x4C6CC8 Networking_RespondToEvent
        public static unsafe UInt32 Networking_RespondToEvent(REGISTERS* R)
        {
            var pEvent = (Pointer<EventExt>)R->ESI;

            if (pEvent.IsNotNull && IsValidType(pEvent.Ref.Type))
            {
                pEvent.Ref.RespondEvent();
            }

            return 0;
        }

        // 0x64B6FE sub_64B660_GetEventSize
        public static unsafe UInt32 sub_64B660_GetEventSize(REGISTERS* R)
        {
            var eventType = (EventTypeExt)(R->EDI & 0xFF);

            if (IsValidType(eventType))
            {
                var eventSize = GetDataSize(eventType);

                R->EDX = (uint)eventSize;
                R->EBP = (uint)eventSize;
                return 0x64B71D;
            }

            return 0;
        }

        // 0x64BE7D sub_64BDD0_GetEventSize1
        public static unsafe UInt32 sub_64BDD0_GetEventSize1(REGISTERS* R)
        {
            var eventType = (EventTypeExt)(R->EDI & 0xFF);

            if (IsValidType(eventType))
            {
                var eventSize = GetDataSize(eventType);

                ((Pointer<int>)R->lea_Stack(0x20)).Ref = eventSize;

                R->ECX = (uint)eventSize;
                R->EBP = (uint)eventSize;
                return 0x64BE97;
            }

            return 0;
        }

        // 0x64C30E sub_64BDD0_GetEventSize2
        public static unsafe UInt32 sub_64BDD0_GetEventSize2(REGISTERS* R)
        {
            var eventType = (EventTypeExt)(R->ESI & 0xFF);

            if (IsValidType(eventType))
            {
                var eventSize = GetDataSize(eventType);

                R->ECX = (uint)eventSize;
                R->EBP = (uint)eventSize;
                return 0x64C321;
            }

            return 0;
        }
    }
}
