// <copyright>
// Copyright by Barefoot Church
//
// Licensed under the Rock Community License (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
// http://www.rockrms.com/license
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// </copyright>

using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.Composition;
using System.Linq;

using Rock;
using Rock.Attribute;
using Rock.Data;
using Rock.Model;
using Rock.Web.Cache;
using Rock.Workflow;

using com.barefootchurch.GroupMe.Helpers;

namespace com.barefootchurch.GroupMe.Workflow.Action
{
    /// <summary>
    /// Adds a person to a group in GroupMe 
    /// </summary>
    [ActionCategory( "GroupMe" )]
    [Description( "Removes a person from a group in GroupMe." )]
    [Export( typeof( ActionComponent ) )]
    [ExportMetadata( "ComponentName", "GroupMe Member Remove" )]

    #region Attributes

    [WorkflowTextOrAttribute(
        textLabel: "GroupMe Group Id",
        attributeLabel: "Attribute Value",
        description: "The ID of the GroupMe group to remove them from.",
        required: false,
        defaultValue: "",
        category: "",
        order: 1,
        key: AttributeKey.GroupId,
        fieldTypeClassNames: new string[] {
            "Rock.Field.Types.TextFieldType",
            "Rock.Field.Types.IntegerFieldType"
        }
    )]

    [WorkflowTextOrAttribute(
        textLabel: "Member Id",
        attributeLabel: "Attribute Value",
        description: "The member's ID in the specified group.",
        required: false,
        defaultValue: "",
        category: "",
        order: 2,
        key: AttributeKey.MemberId,
        fieldTypeClassNames: new string[] {
            "Rock.Field.Types.TextFieldType",
            "Rock.Field.Types.IntegerFieldType"
        }
    )]
    #endregion Attributes

    public class GroupMeMemberRemove : ActionComponent
    {
        #region Keys

        /// <summary>
        /// Attribute Keys
        /// </summary>
        private static class AttributeKey
        {
            /// <summary>
            /// The GroupMe group Id to add them to
            /// </summary>
            public const string GroupId = "GroupId";

            /// <summary>
            /// The member Id returned from GroupMe
            /// </summary>
            public const string MemberId = "MemberId";
        }

        #endregion Keys

        #region Instance Properties

        private WorkflowAction _action;
        private RockContext _rockContext;

        #endregion Instance Properties

        /// <summary>
        /// Executes the specified workflow.
        /// </summary>
        /// <param name="rockContext">The rock context.</param>
        /// <param name="action">The action.</param>
        /// <param name="entity">The entity.</param>
        /// <param name="errorMessages">The error messages.</param>
        /// <returns></returns>
        public override bool Execute( RockContext rockContext, WorkflowAction action, object entity, out List<string> errorMessages )
        {
            errorMessages = new List<string>();

            // Initialize instance properties
            _action = action;
            _rockContext = rockContext;

            int? memberId = GetAttributeValue( _action, AttributeKey.MemberId, true ).AsIntegerOrNull();
            int? groupId = GetAttributeValue( _action, AttributeKey.GroupId, true ).AsIntegerOrNull();

            var globalAttributeCache = GlobalAttributesCache.Get();
            string accessToken = globalAttributeCache.GetValue( SystemKey.GlobalAttribute.GROUPME_API_TOKEN );

            if ( accessToken.IsNullOrWhiteSpace() )
            {
                errorMessages.Add( "GroupMe Access Token Global Attribute is missing or blank" );
            }
            else if ( memberId.IsNullOrZero() )
            {
                errorMessages.Add( "Invalid GroupMe Member Id." );
            }
            else if ( groupId.IsNullOrZero() )
            {
                errorMessages.Add( "Invalid GroupMe Group Id." );
            }

            if ( errorMessages.Any() )
            {
                return false;
            }

            var groupMeAPI = new GroupMeAPI( accessToken );
            var group = groupMeAPI.GetGroup( groupId.Value );

            if ( group == null || group.Name.IsNullOrWhiteSpace() )
            {
                errorMessages.Add( "GroupMe API Error." );
                return false;
            }

            var member = group.Members.AsQueryable().Where( m => m.Id.AsIntegerOrNull() == memberId ).FirstOrDefault();

            if ( member == null )
            {
                errorMessages.Add( "The provided memberId was not found in the group." );
                return false;
            }

            var removed = groupMeAPI.RemoveGroupMember( group, member );
            if ( !removed )
            {
                errorMessages.Add( "GroupMe API Error." );
                return false;
            }

            action.AddLogEntry( $"Successfully removed \"{ member.Nickname }\" from \"{ group.Name }\" on Groupme." );
            
            return true;
        }
    }
}