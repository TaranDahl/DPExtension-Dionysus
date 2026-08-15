using DynamicPatcher;
using Extension.Ext;
using PatcherYRpp;
using System;

namespace Extension.Mutators
{
    [Serializable]
    public class MicroTransactions : Mutator
    {
        public override string UIName => "拿钱说话";
        public override string Description => "对你的单位发出指令会消耗资源，数量取决于该单位的生产价格。";
        public override bool IsAvailableInRPG => false;
        public override int Score => 5;

        public MicroTransactions(Pointer<HouseClass> owner) : base(owner)
        {
        }

        public void OnClick()
        {
            var currentPlayer = HouseClass.Player;
            
            if (currentPlayer.IsNull)
            {
                return;
            }

            if (IsOnTheirSide(currentPlayer))
            {
                EventExt.RaiseMicroTransactions(currentPlayer);
            }
        }

        public static void MouseButtonClick()
        {
            if (ScenarioExt.Global() == null)
            {
                return;
            }

            foreach (var mutator in Mutator.Array)
            {
                if (mutator is MicroTransactions microTransactions)
                {
                    microTransactions.OnClick();
                }
            }
        }
    }
}
