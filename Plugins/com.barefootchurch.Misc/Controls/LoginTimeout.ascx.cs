using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Net;
using System.Web;
using System.Web.UI;

using Rock;
using Rock.Attribute;
using Rock.Utility;
using Rock.Web;
using Rock.Web.UI;
using Rock.Web.UI.Controls;

namespace RockWeb.Plugins.com_BarefootChurch.Misc
{
    [DisplayName( "Login Timeout" )]
    [Category( "Barefoot Church > Misc" )]
    [Description( "Get how long ago the user last logged in, and process that information with a lava template." )]

    #region Block Attributes

    [IntegerField(
        "Timeout Minutes",
        Description = "How many minutes before logging them out?",
        DefaultIntegerValue = 720,
        Order = 1,
        IsRequired = false,
        Key = AttributeKey.TimeoutMinutes
    )]
    [CodeEditorField(
        "Lava Template",
        Description = "Lava template to render to the page. <span class='tip tip-lava'></span>",
        EditorMode = CodeEditorMode.Lava,
        DefaultValue = @"
{% assign loginMinutesAgo = LastLogin | DateDiff:'Now','m' %}

{% if loginMinutesAgo > Timeout %}
    {% assign path = 'Global' | Page:'Url' | Url:'pathandquery' | EscapeDataString %}
    {% assign loginUrl = LoginLink | Append:'?logout=1&timeout=1&returnurl=' | Append:path %}
    {{ loginUrl | PageRedirect }}
{% else %}
    <small style=""float:right"">Hello {{ CurrentPerson.NickName }}, your last login was on {{ LastLogin | Date:'MMM. d' }} at {{ LastLogin | Date:'h:mm tt' | Downcase }}.</small>
{% endif %}
",
        Order = 2,
        IsRequired = false,
        Key = AttributeKey.LavaTemplate
    )]
    [LavaCommandsField(
        "Enabled Lava Commands",
        Description = "The Lava commands that should be enabled for this HTML block.",
        Order = 3,
        IsRequired = false,
        Key = AttributeKey.EnabledLavaCommands
    )]

    #endregion Block Attributes

    public partial class LoginTimeout : RockBlock
    {

        #region Attribute Keys

        private static class AttributeKey
        {
            public const string TimeoutMinutes = "TimeoutMinutes";
            public const string LavaTemplate = "LavaTemplate";
            public const string EnabledLavaCommands = "EnabledLavaCommands";
        }

        #endregion Attribute Keys

        protected override void OnLoad( EventArgs e )
        {
            base.OnLoad( e );

            var loginPageId = this.RockPage.Layout.Site.LoginPageId ?? 3;

            var commonMergeFields = Rock.Lava.LavaHelper.GetCommonMergeFields( this.RockPage, this.CurrentPerson );
            var mergeFields = new Dictionary<string, object>( commonMergeFields );

            // Not sure why I cant get to this property with c#, but I can in lava...
            mergeFields.AddOrReplace( "Identity", HttpContext.Current.User.Identity );
            var lastLogin = "{{ Identity | Property:'Ticket.IssueDate' }}".ResolveMergeFields( mergeFields );
            mergeFields.Remove( "Identity" );

            mergeFields.AddOrReplace( "LastLogin", lastLogin );
            mergeFields.AddOrReplace( "LoginLink", $"/page/{loginPageId}" );
            mergeFields.AddOrReplace( "Timeout", GetAttributeValue( AttributeKey.TimeoutMinutes ).AsInteger() );
            mergeFields.AddOrReplace( "CurrentUser", Rock.Model.UserLoginService.GetCurrentUser() );

            lLoginInfo.Text = GetAttributeValue( AttributeKey.LavaTemplate ).ResolveMergeFields( mergeFields, GetAttributeValue( AttributeKey.EnabledLavaCommands ) );
        }

    }
}