using System.Collections.Generic;
using System.Linq;
using System.Text;
using Sandbox.Game.GameSystems.BankingAndCurrency;
using Sandbox.Game.World;
using VRage.Game.ModAPI;

namespace SensEssentials.Commands;

/// <summary>
/// The !economy commands.
/// </summary>
public sealed class EcoActions(ICommandContext context) : CommandActions(context) {

    public void EcoGive(string player, long amount, bool onlyOnline = false, bool excludeNpcs = true) {

        if (amount <= 0) {
            Context.Respond("Amount must be greater than 0");
            return;
        }

        if(!TryFindPlayerIdentities(player, onlyOnline, excludeNpcs, out List<long> foundIdentities)) {
            Context.Respond("Player cannot be found!");
            return;
        }

        int changedIdentities = 0;
        int cappedIdentities = 0;

        foreach (long identityId in foundIdentities) {

            if (WouldOverflow(identityId, amount))
                cappedIdentities++;
            ChangeBalance(identityId, amount);

            Notify.Player(Utilities.GetSteamId(identityId), $"{amount:#,##0} credits have been added to your virtual account", 10000);

            changedIdentities++;
        }

        Context.Respond($"{amount:#,##0} credits given to {changedIdentities:#,##0} account(s)"
            + (cappedIdentities > 0 ? $", {cappedIdentities:#,##0} of them capped at the largest possible balance" : ""));
    }

    public void EcoTake(string player, long amount, bool onlyOnline = false, bool excludeNpcs = true) {

        if (amount <= 0) {
            Context.Respond("Amount must be greater than 0");
            return;
        }

        if (!TryFindPlayerIdentities(player, onlyOnline, excludeNpcs, out List<long> foundIdentities)) {
            Context.Respond("Player cannot be found!");
            return;
        }

        int changedIdentities = 0;

        foreach (long identityId in foundIdentities) {

            ChangeBalance(identityId, -amount);

            Notify.Player(Utilities.GetSteamId(identityId), $"{amount:#,##0} credits have been taken from your virtual account", 10000);

            changedIdentities++;
        }

        Context.Respond($"{amount:#,##0} credits taken from {changedIdentities:#,##0} account(s)");
    }

    public void EcoSet(string player, long amount, bool onlyOnline = false, bool excludeNpcs = true) {

        if (amount < 0) {
            Context.Respond("Balance cannot be negative");
            return;
        }

        if (!TryFindPlayerIdentities(player, onlyOnline, excludeNpcs, out List<long> foundIdentities)) {
            Context.Respond("Player cannot be found!");
            return;
        }

        int changedIdentities = 0;

        foreach (long identityId in foundIdentities) {

            long balance = MyBankingSystem.GetBalance(identityId);

            ChangeBalance(identityId, -(balance - amount));

            Notify.Player(Utilities.GetSteamId(identityId), $"Your balance has been set to {amount:#,##0} credits!", 10000);

            changedIdentities++;
        }

        Context.Respond($"Balance(s) set to {amount:#,##0} on {changedIdentities:#,##0} accounts");
    }

    public void EcoReset(string player, bool onlyOnline = false, bool excludeNpcs = true) {
        EcoSet(player, 10_000, onlyOnline, excludeNpcs);
    }

    public void EcoTop(bool onlyOnline = false, bool excludeNpcs = true) {

        TryFindPlayerIdentities("*", onlyOnline, excludeNpcs, out List<long> foundIdentities);

        var players = MySession.Static.Players;

        Dictionary<MyIdentity, long> balances = new Dictionary<MyIdentity, long>();
        foreach (long identityId in foundIdentities) {

            var identity = players.TryGetIdentity(identityId);
            if (identity == null)
                continue;
            long balance = MyBankingSystem.GetBalance(identityId);

            balances[identity] = balance;
        }

        StringBuilder ecodata = new StringBuilder();
        ecodata.AppendLine("Summary of balances across the server");

        var sorted = balances.OrderByDescending(x => x.Value).ThenBy(x => x.Key.DisplayName);
        foreach (var value in sorted) {

            var identity = value.Key;

            ecodata.AppendLine($"Player: {identity.DisplayName} - Balance: {value.Value:#,##0}");
        }

        Show("Public balance list", "List of players and their credit balances", ecodata.ToString());
    }

    public void EcoCheck(string player) {

        var p = Utilities.GetIdentityByNameOrIds(player);
        if (p == null) {
            Context.Respond("Player cannot be found!");
            return;
        }

        long balance = MyBankingSystem.GetBalance(p.IdentityId);

        Context.Respond($"{p.DisplayName}'s balance is {balance:#,##0} credits");
    }

    public void EcoPay(string player, long amount) {
        if (amount <= 0) {
            Context.Respond("Amount must be greater than 0");
            return;
        }
        if (Context.Player == null) {
            Context.Respond("Console cannot execute this command");
            return;
        }
            
        /* We are purposely keeping the online check in this method. Otherwise it could cause confusion with players. */
        var p = Utilities.GetPlayerByNameOrId(player);
        if (p == null) {
            Context.Respond("Player is not online or cannot be found!");
            return;
        }

        var fromIdentitiyId = Context.Player.Identity.IdentityId;
        var toIdentitiyId = p.Identity.IdentityId;

        if (fromIdentitiyId == toIdentitiyId) {
            Context.Respond("You cannot pay yourself!");
            return;
        }

        var finalFromBalance = MyBankingSystem.GetBalance(fromIdentitiyId) - amount;

        if(finalFromBalance < 0) {
            Context.Respond($"Sorry, but you are short {-finalFromBalance:#,##0} credits!");
            return;
        }

        if (WouldOverflow(toIdentitiyId, amount)) {
            Context.Respond($"{p.DisplayName} cannot hold that many credits.");
            return;
        }

        // ChangeBalance updates the server and broadcasts the new balance to clients.
        if (!MyBankingSystem.ChangeBalance(fromIdentitiyId, -amount)) {
            Context.Respond("The payment could not be made.");
            return;
        }

        if (!MyBankingSystem.ChangeBalance(toIdentitiyId, amount)) {
            MyBankingSystem.ChangeBalance(fromIdentitiyId, amount);
            Context.Respond("The payment could not be made.");
            return;
        }
        Notify.Player(p.SteamUserId, $"You have received {amount:#,##0} credits from {Context.Player.DisplayName}!", 10000);
        Notify.Player(Context.Player.SteamUserId, $"You have sent {amount:#,##0} credits to {p.DisplayName}!", 10000);
    }

    /// <summary>
    /// Changes the balance of the given identity by the passed amount: positive adds credits, negative takes them.
    /// The balance stays between 0 and <see cref="long.MaxValue"/>. The game does not check the upper end, and a
    /// balance past it would wrap around to a large negative number.
    /// </summary>
    private static void ChangeBalance(long identityId, long amount) {

        long balance = MyBankingSystem.GetBalance(identityId);

        if (amount < 0 && balance + amount < 0)
            amount = -balance;
        else if (amount > 0 && amount > long.MaxValue - balance)
            amount = long.MaxValue - balance;

        if (amount != 0)
            MyBankingSystem.ChangeBalance(identityId, amount);
    }

    /// <summary>True when adding the amount would take the balance past <see cref="long.MaxValue"/>.</summary>
    private static bool WouldOverflow(long identityId, long amount) =>
        amount > long.MaxValue - MyBankingSystem.GetBalance(identityId);

    /// <summary>
    /// This method take all identities of the server and assembles a list with the respective identityIDs. 
    /// It is possible to filter this list to not contain NPCs or only contain players that are currently online.
    /// 
    /// Returns true if the given playerName exists and could be found, or the playerName is "*" false otherwise. 
    /// Even if true is returned the list of foundIdentities can be empty if the online or npc filters got applied.
    /// </summary>
    private bool TryFindPlayerIdentities(string playerName, bool onlyOnline, bool excludeNpcs, out List<long> foundIdentities) {

        var relevantIdentities = new List<long>();
        var players = MySession.Static.Players;

        if (playerName != "*") {

            IMyIdentity? identity = Utilities.GetIdentityByNameOrIds(playerName);

            if (identity == null) 
            {
                foundIdentities = relevantIdentities;
                return false;
            }

            relevantIdentities.Add(identity.IdentityId);

        } 
        else 
        {
            relevantIdentities.AddRange(players.GetAllIdentities()
                .Select(identity => identity.IdentityId));
        }

        IEnumerable<long> identitiesToCheck = relevantIdentities;

        if (onlyOnline)
            identitiesToCheck = identitiesToCheck.Where(identityId => players.IsPlayerOnline(identityId));

        if (excludeNpcs)
            identitiesToCheck = identitiesToCheck.Where(identityId => !Utils.Ownership.IsNpcIdentity(identityId));

        foundIdentities = identitiesToCheck.ToList();

        return true;
    }
}