﻿using Discord;
using Discord.Commands;
using System;
using System.Threading.Tasks;
using DEA.Database.Repository;
using System.Linq;
using DEA.Database.Models;
using MongoDB.Driver;
using DEA.Resources;
using DEA.Services;

namespace DEA.Modules
{
    public class Gangs : DEAModule
    {

        [Command("CreateGang")]
        [Require(Attributes.NoGang)]
        [Summary("Allows you to create a gang at a hefty price.")]
        [Remarks("Create <Name>")]
        public async Task ResetCooldowns([Remainder] string name)
        {
            var user = UserRepository.FetchUser(Context);
            if (user.Cash < Config.GANG_CREATION_COST)
                Error($"You do not have {Config.GANG_CREATION_COST.ToString("C", Config.CI)}. Balance: {user.Cash.ToString("C", Config.CI)}.");
            var gang = GangRepository.CreateGang(Context.User.Id, Context.Guild.Id, name);
            await UserRepository.EditCashAsync(Context, -Config.GANG_CREATION_COST);
            await Reply($"You have successfully created the {gang.Name} gang!");
        }

        [Command("AddGangMember")]
        [Require(Attributes.InGang, Attributes.GangLeader)]
        [Summary("Allows you to add a member to your gang.")]
        [Remarks("AddGangMember <@GangMember>")]
        public async Task AddToGang(IGuildUser user)
        {
            if (GangRepository.InGang(user.Id, Context.Guild.Id)) Error("This user is already in a gang.");
            if (GangRepository.IsFull(Context.User.Id, Context.Guild.Id)) Error("Your gang is already full!");
            GangRepository.AddMember(Context.User.Id, Context.Guild.Id, user.Id);
            await Reply($"{user} is now a new member of your gang!");
            var channel = await user.CreateDMChannelAsync();
            await ResponseMethods.DM(channel, $"Congrats! You are now a member of {GangRepository.FetchGang(Context).Name}!");
        }

        [Command("Gang")]
        [Summary("Gives you all the info about any gang.")]
        [Remarks("Gang [Gang name]")]
        public async Task Gang([Remainder] string gangName = null)
        {
            Gang gang;
            if (gangName == null) gang = GangRepository.FetchGang(Context);
            else gang = GangRepository.FetchGang(gangName, Context.Guild.Id);
            var members = "";
            var leader = "";
            if (Context.Guild.GetUser(gang.LeaderId) != null) leader = $"<@{gang.LeaderId}>";
            foreach (var member in gang.Members)
                if (Context.Guild.GetUser(member) != null) members += $"<@{member}>, ";
            if (members.Length > 2) members = $"__**Members:**__ {members.Substring(0, members.Length - 2)}\n";
            var description = $"__**Leader:**__ {leader}\n" + members + $"__**Wealth:**__ {gang.Wealth.ToString("C", Config.CI)}\n" +
                              $"__**Interest rate:**__ {Services.Math.CalculateIntrestRate(gang.Wealth).ToString("P")}";
            await Send(description, gang.Name);
        }

        [Command("GangLb")]
        [Alias("gangs")]
        [Summary("Shows the wealthiest gangs.")]
        [Remarks("Gangs")]
        public async Task Ganglb()
        {
            var gangs = DEABot.Gangs.Find(y => y.GuildId == Context.Guild.Id).ToList();

            if (gangs.Count == 0) Error("There aren't any gangs yet.");

            var sortedGangs = gangs.OrderByDescending(x => x.Wealth).ToList();
            string description = "";

            for (int i = 0; i < sortedGangs.Count(); i++)
            {
                if (i + 1 >= Config.GANGSLB_CAP) break;
                description += $"{i + 1}. {sortedGangs[i].Name}: {sortedGangs[i].Wealth.ToString("C", Config.CI)}\n";
            }

            await Send(description, "The Wealthiest Gangs");
        }

        [Command("LeaveGang")]
        [Require(Attributes.InGang)]
        [Summary("Allows you to break all ties with a gang.")]
        [Remarks("LeaveGang")]
        public async Task LeaveGang()
        {
            var gang = GangRepository.FetchGang(Context);
            var prefix = GuildRepository.FetchGuild(Context.Guild.Id).Prefix;
            if (gang.LeaderId == Context.User.Id)
                Error($"You may not leave a gang if you are the owner. Either destroy the gang with the `{prefix}DestroyGang` command, or " +
                                    $"transfer the ownership of the gang to another member with the `{prefix}TransferLeadership` command.");
            GangRepository.RemoveMember(Context.User.Id, Context.Guild.Id);
            await Reply($"You have successfully left {gang.Name}");
            var channel = await Context.Client.GetUser(gang.LeaderId).CreateDMChannelAsync();
            await ResponseMethods.DM(channel, $"{Context.User} has left {gang.Name}.");
        }

        [Command("KickGangMember")]
        [Require(Attributes.InGang, Attributes.GangLeader)]
        [Summary("Kicks a user from your gang.")]
        [Remarks("KickGangMember")]
        public async Task KickFromGang([Remainder] IGuildUser user)
        {
            if (user.Id == Context.User.Id)
                Error("You may not kick yourself!");
            if (!GangRepository.IsMemberOf(Context.User.Id, Context.Guild.Id, user.Id))
                Error("This user is not a member of your gang!");
            var gang = GangRepository.FetchGang(Context);
            GangRepository.RemoveMember(user.Id, Context.Guild.Id);
            await Reply($"You have successfully kicked {user} from {gang.Name}");
            var channel = await user.CreateDMChannelAsync();
            await ResponseMethods.DM(channel, $"You have been kicked from {gang.Name}.");
        }

        [Command("DestroyGang")]
        [Require(Attributes.InGang, Attributes.GangLeader)]
        [Summary("Destroys a gang entirely taking down all funds with it.")]
        [Remarks("DestroyGang")]
        public async Task DestroyGang()
        {
            GangRepository.DestroyGang(Context.User.Id, Context.Guild.Id);
            await Reply($"You have successfully destroyed your gang.");
        }

        [Command("ChangeGangName")]
        [Alias("ChangeName")]
        [Require(Attributes.InGang, Attributes.GangLeader)]
        [Summary("Changes the name of your gang.")]
        [Remarks("ChangeGangName <New name>")]
        public async Task ChangeGangName([Remainder] string name)
        {
            var user = UserRepository.FetchUser(Context);
            if (user.Cash < Config.GANG_NAME_CHANGE_COST)
                Error($"You do not have {Config.GANG_NAME_CHANGE_COST.ToString("C", Config.CI)}. Balance: {user.Cash.ToString("C", Config.CI)}.");
            var gangs = DEABot.Gangs.Find(y => y.GuildId == Context.Guild.Id).ToList();
            if (gangs.Any(x => x.Name.ToLower() == name.ToLower())) Error($"There is already a gang by the name {name}.");
            await UserRepository.EditCashAsync(Context, -Config.GANG_NAME_CHANGE_COST);
            GangRepository.Modify(DEABot.GangUpdateBuilder.Set(x => x.Name, name), Context);
            await Reply($"You have successfully changed your gang name to {name} at the cost of {Config.GANG_NAME_CHANGE_COST.ToString("C", Config.CI)}.");
        }

        [Command("TransferLeadership")]
        [Require(Attributes.InGang, Attributes.GangLeader)]
        [Summary("Transfers the leadership of your gang to another member.")]
        [Remarks("TransferLeadership <@GangMember>")]
        public async Task TransferLeadership(IGuildUser user)
        {
            if (user.Id == Context.User.Id) Error("You are already the leader of this gang!");
            var gang = GangRepository.FetchGang(Context);
            if (!GangRepository.IsMemberOf(Context.User.Id, Context.Guild.Id, user.Id)) Error("This user is not a member of your gang!");
            for (int i = 0; i < gang.Members.Length; i++)
                if (gang.Members[i] == user.Id)
                {
                    gang.Members[i] = Context.User.Id;
                    GangRepository.Modify(DEABot.GangUpdateBuilder.Combine(
                        DEABot.GangUpdateBuilder.Set(x => x.LeaderId, user.Id),
                        DEABot.GangUpdateBuilder.Set(x => x.Members, gang.Members)), Context);
                    break;
                }
            await Reply($"You have successfully transferred the leadership of {gang.Name} to {user.Mention}");
        }

        [Command("Deposit")]
        [Require(Attributes.InGang)]
        [Summary("Deposit cash into your gang's funds.")]
        [Remarks("Deposit <Cash>")]
        public async Task Deposit(decimal cash)
        {
            var user = UserRepository.FetchUser(Context);
            if (cash < Config.MIN_DEPOSIT) Error($"The lowest deposit is {Config.MIN_DEPOSIT.ToString("C", Config.CI)}.");
            if (user.Cash < cash) Error($"You do not have enough money. Balance: {user.Cash.ToString("C", Config.CI)}.");
            await UserRepository.EditCashAsync(Context, -cash);
            var gang = GangRepository.FetchGang(Context);
            GangRepository.Modify(DEABot.GangUpdateBuilder.Set(x => x.Wealth, gang.Wealth + cash), Context.User.Id, Context.Guild.Id);
            await Reply($"You have successfully deposited {cash.ToString("C", Config.CI)}. " +
                        $"{gang.Name}'s Wealth: {(gang.Wealth + cash).ToString("C", Config.CI)}");
        }

        [Command("Withdraw")]
        [Require(Attributes.InGang)]
        [RequireCooldown]
        [Summary("Withdraw cash from your gang's funds.")]
        [Remarks("Withdraw <Cash>")]
        public async Task Withdraw(decimal cash)
        {
            var gang = GangRepository.FetchGang(Context);
            var user = UserRepository.FetchUser(Context);
            if (cash < Config.MIN_WITHDRAW) Error($"The minimum withdrawal is {Config.MIN_WITHDRAW.ToString("C", Config.CI)}.");
            if (cash > gang.Wealth * Config.WITHDRAW_CAP)
                Error($"You may only withdraw {Config.WITHDRAW_CAP.ToString("P")} of your gang's wealth, " +
                                    $"that is {(gang.Wealth * Config.WITHDRAW_CAP).ToString("C", Config.CI)}.");
            UserRepository.Modify(DEABot.UserUpdateBuilder.Set(x => x.Withdraw, DateTime.UtcNow), Context);
            GangRepository.Modify(DEABot.GangUpdateBuilder.Set(x => x.Wealth, gang.Wealth - cash), Context.User.Id, Context.Guild.Id);
            await UserRepository.EditCashAsync(Context, +cash);
            await Reply($"You have successfully withdrawn {cash.ToString("C", Config.CI)}. " +
                        $"{gang.Name}'s Wealth: {(gang.Wealth - cash).ToString("C", Config.CI)}");
        }

    }
}