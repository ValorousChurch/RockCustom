// <copyright>
// Copyright 2013 by the Spark Development Network
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
// http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// </copyright>
//
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
    /// Block to display a count of the connection requests assigned to the user current user.
    /// </summary>
    [DisplayName( "My Connection Alerts" )]
    [Category( "Barefoot Church" )]
    [Description( "Block to display a count of the connection requests assigned to the user current users." )]
    [LinkedPage( "Listing Page", "Page used to view all connection requests assigned to the current user.", false, "530860ED-BC73-4A43-8E7C-69533EF2B6AD" )]
    [BooleanField( "Critical Only", "Only count critical connections", true, IsRequired = true )]
    [IntegerField( "Cache Duration", "Number of seconds to cache the content per person.", false, 60, "", 2 )]
    public partial class ConnectionAlert : Rock.Web.UI.RockBlock
    {
        protected override void OnLoad( EventArgs e )
        {
            base.OnLoad( e );

            connectionCount();
        }

        protected void connectionCount()
        {
            // Check for current person
            if ( CurrentPersonAliasId.HasValue )
            {
                int cacheDuration = GetAttributeValue( "CacheDuration" ).AsInteger();
                string cacheKey = "MyAlerts:ConnectionCount:PersonAliasId:" + CurrentPersonAliasId.ToString();
                int? activeConnections = null;
                if ( cacheDuration > 0 )
                {
                    activeConnections = this.GetCacheItem( cacheKey ) as int?;
                }

                if ( !activeConnections.HasValue )
                {
                    using ( var rockContext = new RockContext() )
                    {
                        // Return the count of connection requests assigned to the current user that are in a critical status
                        activeConnections = GetConnections( rockContext ).Count();
                        if ( cacheDuration > 0 )
                        {
                            this.AddCacheItem( cacheKey, activeConnections, cacheDuration );
                        }
                    }
                }

                // set the default display
                var spanLiteral = "";
                if ( activeConnections > 0 )
                {
                    // add the count of how many workflows need to be assigned/completed
                    spanLiteral = string.Format( "<span class='badge badge-info'>{0}</span>", activeConnections );
                }

                lbConnectionListingPage.Controls.Add( new LiteralControl( spanLiteral ) );
            }
        }

        /// <summary>
        /// Navigates to the Listing page.
        /// </summary>
        protected void lbConnectionListingPage_Click( object sender, EventArgs e )
        {
            SetUserPreference( "MyConnectionOpportunities_Toggle", "true" );
            SetUserPreference( "MyConnectionOpportunities_SelectedOpportunity", null );

            NavigateToLinkedPage( "ListingPage" );
        }

        /// <summary>
        /// Gets a list of all the connection requests for the current person that have a critical status
        /// </summary>
        /// <param name="rockContext"></param>
        /// <returns></returns>
        private List<ConnectionRequest> GetConnections( RockContext rockContext )
        {
            var connections = new List<ConnectionRequest>();
            if ( CurrentPerson != null )
            {
                connections = RockPage.GetSharedItem( "ActiveConnections" ) as List<ConnectionRequest>;


                if ( connections == null )
                {
                    var query = new ConnectionRequestService( rockContext ).Queryable()
                        .Where( r => r.ConnectionState == 0 ) // Active
                        .Where( r => r.ConnectorPersonAliasId == CurrentPersonAliasId );
                    if ( GetAttributeValue( "CriticalOnly").AsBoolean() ) { query = query.Where( r => r.ConnectionStatus.IsCritical == true ); }
                    connections = query.ToList();
                    RockPage.SaveSharedItem( "ActiveConnections", connections );
                }
            }
            return connections;

        }
    }
}