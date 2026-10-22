using Rs13.Packs;

namespace BloodMoney;

// Wallets live on player records, found by the player's id
public sealed partial class WalletSystem : EntitySystem
{
    public Dictionary<PlayerId, Entity<PlayerRecord, Wallet>> ByPlayer()
    {
        var found = new Dictionary<PlayerId, Entity<PlayerRecord, Wallet>>();
        var records = EntityQueryEnumerator<PlayerRecord, Wallet>();
        while (records.MoveNext(out var uid, out var record, out var wallet))
        {
            found[record.Id] = (uid, record, wallet);
        }
        return found;
    }

    public bool TryFind(PlayerId player, out Entity<PlayerRecord, Wallet> found) => ByPlayer().TryGetValue(player, out found);

    // Balance and earned move together on a credit, and never wrap
    public static void Credit(Wallet wallet, uint amount)
    {
        wallet.Balance = Saturate(wallet.Balance, amount);
        wallet.Earned = Saturate(wallet.Earned, amount);
    }

    public static uint Saturate(uint a, uint b) => (uint)Math.Min((ulong)a + b, uint.MaxValue);
}
