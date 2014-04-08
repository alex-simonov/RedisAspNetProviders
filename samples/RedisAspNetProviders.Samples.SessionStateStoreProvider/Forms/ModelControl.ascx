<%@ Control Language="C#" AutoEventWireup="true" CodeBehind="ModelControl.ascx.cs" Inherits="RedisAspNetProviders.Samples.SessionStateStoreProvider.Forms.ModelControl" %>

<asp:FormView runat="server" ID="ModelView" DefaultMode="ReadOnly" SelectMethod="GetModel" ItemType="RedisAspNetProviders.Samples.SessionStateStoreProvider.Models.Model">
    <ItemTemplate>
        <p>DateTime: <%# Eval("DateTime") %></p>
        <p>FromSession: <%# Eval("FromSession") %></p>
        <asp:Button runat="server" OnClientClick=" window.document.reload() " Text="Refresh"/>
        <asp:Button runat="server" OnClick="UpdateSession" Text="Set DateTime.Now to Session"/>
        <asp:Button runat="server" OnClick="ClearSession" Text="Clear Session"/>
        <asp:Button runat="server" OnClick="AbandonSession" Text="Abandon Session"/>
    </ItemTemplate>
</asp:FormView>