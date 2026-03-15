using UnityEngine;
using System.Collections.Generic;

public class SwapCardsSkill : BaseSkill
{
    public SwapCardsSkill()
    {
        skillID = 3;
        skillName = "底牌重铸 (Swap)";
        energyCost = 5;      // 消耗 5 点能量（大招级别）
        castTime = 3.0f;     // 施法 3 秒，非常容易被打断！
    }

    public override void Execute(PokerPlayer caster, PokerPlayer target, ServerGameManager serverContext)
    {
        // 确保目标有底牌可以换
        if (target.serverHand.Count < 2) return;

        // 1. 从牌库顶抽两张新牌
        Card newCard1 = serverContext.DrawCardFromDeck();
        Card newCard2 = serverContext.DrawCardFromDeck();

        // 2. 替换服务器端记录的底牌（旧牌相当于直接丢弃了）
        target.serverHand.Clear();
        target.serverHand.Add(newCard1);
        target.serverHand.Add(newCard2);

        // 3. 核心：通知该玩家的客户端，重新渲染底牌画面！
        // 因为你之前写过 TargetReceiveHoleCards，客户端收到后会自动覆盖旧图片
        target.TargetReceiveHoleCards(target.connectionToClient, newCard1, newCard2);

        // 4. 发送悄悄话提示
        target.TargetReceiveSkillMessage(target.connectionToClient, $"换牌成功！你的新底牌是：{newCard1} 和 {newCard2}");
    }
}