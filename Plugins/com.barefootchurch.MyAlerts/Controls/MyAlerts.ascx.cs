using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Web.UI;
using System.Web.UI.WebControls;

using Rock;
using Rock.Attribute;
using Rock.Data;
using Rock.Model;
using Rock.Web.Cache;

namespace RockWeb.Plugins.com_barefootchurch.MyAlerts
{
    /// <summary>
    /// Block to display the workflow types that user is authorized to view, and the activities that are currently assigned to the user.
    /// </summary>
    [DisplayName( "My Alerts" )]
    [Category( "Barefoot Church" )]
    [Description( "Block to display a count of items that are assigned to the current user." )]
    [LinkedPage(
        "Dashboard Page",
        Description = "Page used to view all items assigned to the current user.",
        IsRequired = false,
        DefaultValue = "ae1818d8-581c-4599-97b9-509ea450376a",
        Order = 0,
        Key = AttributeKey.DashboardPage )]
    [IntegerField(
        "Cache Duration",
        Description = "Number of seconds to cache the content per person.",
        IsRequired = false,
        DefaultValue = "60",
        Order = 1,
        Key = AttributeKey.CacheDuration )]

    // Workflow
    [BooleanField(
        "Include Workflows",
        Category = "Workflows",
        Description = "Should we include workflows in the count?",
        IsRequired = true,
        DefaultValue = "true",
        Order = 0,
        Key = AttributeKey.IncludeWorkflows )]
    [CategoryField(
        "Workflow Categories",
        Description = "Only count workflows in the following categories (blank = all).",
        Category = "Workflows",
        AllowMultiple = true,
        EntityTypeName = "Rock.Model.WorkflowType",
        IsRequired = false,
        DefaultValue = "",
        Order = 1,
        Key = AttributeKey.WorkflowCategories )]
    [BooleanField(
        "Include Child Categories",
        Category = "Workflows",
        Description = "Should descendant categories of the selected Categories be included?",
        IsRequired = true,
        DefaultValue = "true",
        Order = 2,
        Key = AttributeKey.IncludeChildCategories )]
    
    // Connection Request
    [BooleanField(
        "Include Connections",
        Category = "Connections",
        Description = "Should we include connections in the count?",
        IsRequired = true,
        DefaultValue = "true",
        Order = 0,
        Key = AttributeKey.IncludeConnections )]
    [ConnectionTypesField(
        "Connection Types",
        Category = "Connections",
        Description = "Only count connections in the following categories (blank = all).",
        IsRequired = false,
        DefaultValue = "",
        Order = 1,
        Key = AttributeKey.ConnectionTypes )]
    [BooleanField(
        "Critical Connections Only",
        Category = "Connections",
        Description = "Only count critical connections",
        IsRequired = true,
        DefaultValue = "true",
        Order = 2,
        Key = AttributeKey.CriticalConnectionsOnly )]

    // Project Management
    [BooleanField(
        "Include Projects",
        Category = "ProjectManagement",
        Description = "Should we include projects in the count?",
        IsRequired = true,
        DefaultValue = "true",
        Order = 0,
        Key = AttributeKey.IncludeProjects )]
    [BooleanField(
        "Include Tasks",
        Category = "ProjectManagement",
        Description = "Should we include tasks in the count?",
        IsRequired = true,
        DefaultValue = "true",
        Order = 1,
        Key = AttributeKey.IncludeTasks )]
    
    public partial class MyAlerts : Rock.Web.UI.RockBlock
    {
        public static class AttributeKey
        {
            public const string DashboardPage = "DashboardPage";
            public const string CacheDuration = "CacheDuration";
            public const string IncludeWorkflows = "IncludeWorkflows";
            public const string WorkflowCategories = "WorkflowCategories";
            public const string IncludeChildCategories = "IncludeChildCategories";
            public const string IncludeConnections = "IncludeConnections";
            public const string ConnectionTypes = "ConnectionTypes";
            public const string CriticalConnectionsOnly = "CriticalConnectionsOnly";
            public const string IncludeProjects = "IncludeProjects";
            public const string IncludeTasks = "IncludeTasks";
            public const string ProjectTypes = "ProjectTypes";
        }
        protected override void OnInit( EventArgs e )
        {
            base.OnInit( e );

            RockPage.AddCSSLink( "~/Plugins/com_barefootchurch/MyAlerts/MyAlerts.css" );
            RockPage.AddScriptLink( "~/Plugins/com_barefootchurch/MyAlerts/MyAlerts.js" );
        }

        protected override void OnLoad( EventArgs e )
        {
            base.OnLoad( e );

            if ( CurrentPersonAliasId.HasValue )
            {
                int cacheDuration = GetAttributeValue( AttributeKey.CacheDuration ).AsInteger();
                string cacheKey = "MyAlerts:PersonAliasId:" + CurrentPersonAliasId.ToString();
                int? totalAlerts = null;

                if ( cacheDuration > 0 )
                {
                    totalAlerts = GetCacheItem( cacheKey ) as int?;
                }

                if ( !totalAlerts.HasValue )
                {
                    using ( var rockContext = new RockContext() )
                    {
                        totalAlerts = 0;

                        if( GetAttributeValue( AttributeKey.IncludeWorkflows ).AsBoolean() )
                        {
                            totalAlerts += GetWorkflows( rockContext ).Count();
                        }

                        if ( GetAttributeValue( AttributeKey.IncludeConnections ).AsBoolean() )
                        {
                            totalAlerts += GetConnections( rockContext ).Count();
                        }

                        if ( GetAttributeValue( AttributeKey.IncludeProjects ).AsBoolean() )
                        {
                           // totalAlerts += GetProjectCount( rockContext );
                        }

                        if ( GetAttributeValue( AttributeKey.IncludeTasks ).AsBoolean() )
                        {
                            //totalAlerts += GetTaskCount( rockContext );
                        }
                    }
                }

                if ( cacheDuration > 0 )
                {
                    AddCacheItem( cacheKey, totalAlerts, cacheDuration );
                }

                if ( totalAlerts > 0 )
                {
                    // add a badge for the count of alerts
                    var spanLiteral = string.Format( "<span class='badge badge-danger'>{0}</span>", totalAlerts );
                    lbAlerts.Controls.Add( new LiteralControl( spanLiteral ) );
                }

            }
        }

        /// <summary>
        /// Navigates to the Dashboard page.
        /// </summary>
        protected void lbAlerts_Click( object sender, EventArgs e )
        {
            NavigateToLinkedPage( AttributeKey.DashboardPage );
        }

        /// <summary>
        /// Returns a list of the actions assigned to the current person
        /// </summary>
        /// <param name="rockContext"></param>
        /// <returns></returns>
        private List<WorkflowAction> GetWorkflows( RockContext rockContext )
        {
            var formActions = new List<WorkflowAction>();

            if ( CurrentPerson != null )
            {
                // Get all of the active form actions that user is assigned to and authorized to view
                formActions = GetActiveForms( rockContext );

                // If a category filter was specified, filter list by selected categories
                var categoryIds = GetCategories( rockContext );
                if ( categoryIds.Any() )
                {
                    formActions = formActions
                        .Where( a =>
                            a.ActionType.ActivityType.WorkflowType.CategoryId.HasValue &&
                            categoryIds.Contains( a.ActionType.ActivityType.WorkflowType.CategoryId.Value ) )
                        .ToList();
                }
            }

            return formActions;
        }

        /// <summary>
        /// Gets a list of all the active workflow actions for the current person
        /// </summary>
        /// <param name="rockContext"></param>
        /// <returns></returns>
        private List<WorkflowAction> GetActiveForms( RockContext rockContext )
        {
            var formActions = RockPage.GetSharedItem( "ActiveForms" ) as List<WorkflowAction>;

            if ( formActions == null )
            {
                formActions = new WorkflowActionService( rockContext ).GetActiveForms( CurrentPerson );
                RockPage.SaveSharedItem( "ActiveForms", formActions );
            }

            // find first form for each activity
            var firstForms = new List<WorkflowAction>();
            foreach( var activityId in formActions.Select( a => a.ActivityId ).Distinct().ToList() )
            {
                firstForms.Add( formActions.First( a => a.ActivityId == activityId ) );
            }

            return firstForms;

        }

        /// <summary>
        /// Gets a list of all the connection requests for the current person that have a critical status
        /// </summary>
        /// <param name="rockContext"></param>
        /// <returns></returns>
        private List<ConnectionRequest> GetConnections(RockContext rockContext)
        {
            var connections = RockPage.GetSharedItem( "ActiveConnections" ) as List<ConnectionRequest>;

            if ( connections == null )
            {
                var query = new ConnectionRequestService( rockContext ).Queryable()
                    .Where(
                        r => r.ConnectionState == ConnectionState.Active ||
                        ( r.ConnectionState == ConnectionState.FutureFollowUp && r.FollowupDate < RockDateTime.Now )
                    )
                    .Where( r => r.ConnectorPersonAliasId == CurrentPersonAliasId );

                var types = GetAttributeValue( AttributeKey.ConnectionTypes ).SplitDelimitedValues().ToList();
                if ( types.Count() > 0 )
                {
                    query = query.Where( r => types.Contains( r.ConnectionOpportunity.ConnectionType.Guid.ToString() ) );
                }

                if ( GetAttributeValue( AttributeKey.CriticalConnectionsOnly ).AsBoolean() )
                {
                    query = query.Where( r => r.ConnectionStatus.IsCritical == true );
                }
                
                connections = query.ToList();

                RockPage.SaveSharedItem( "ActiveConnections", connections );
            }
            return connections;

        }

        /// <summary>
        /// Returns a list of all category ids
        /// </summary>
        /// <param name="rockContext"></param>
        /// <returns></returns>
        private List<int> GetCategories( RockContext rockContext )
        {
            int entityTypeId = EntityTypeCache.Get( typeof( WorkflowType ) ).Id;

            var selectedCategories = new List<Guid>();
            GetAttributeValue( AttributeKey.WorkflowCategories )
                .SplitDelimitedValues()
                .ToList()
                .ForEach( c => selectedCategories.Add( c.AsGuid() ) );

            bool includeChildCategories = GetAttributeValue( AttributeKey.IncludeChildCategories ).AsBoolean();

            return GetCategoryIds( new List<int>(), new CategoryService( rockContext ).GetNavigationItems( entityTypeId, selectedCategories, includeChildCategories, CurrentPerson ) );
        }

        /// <summary>
        /// Recursively gets all category ids in the tree of categories passed
        /// </summary>
        /// <param name="ids"></param>
        /// <param name="categories"></param>
        /// <returns></returns>
        private List<int> GetCategoryIds( List<int> ids, List<CategoryNavigationItem> categories )
        {
            foreach ( var categoryNavItem in categories )
            {
                ids.Add( categoryNavItem.Category.Id );
                GetCategoryIds( ids, categoryNavItem.ChildCategories );
            }

            return ids;
        }
    }
}