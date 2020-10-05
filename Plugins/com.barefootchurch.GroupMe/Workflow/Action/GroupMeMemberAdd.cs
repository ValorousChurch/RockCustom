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
using System.Data.Entity;
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
    [Description( "Adds a person to a group in GroupMe." )]
    [Export( typeof( ActionComponent ) )]
    [ExportMetadata( "ComponentName", "GroupMe Member Add" )]

    #region Attributes

    [WorkflowAttribute(
        name: "Person",
        description: "The person to add to the group.",
        required: true,
        defaultValue: "",
        category: "",
        order: 2,
        key: AttributeKey.Person,
        fieldTypeClassNames: new string[] { "Rock.Field.Types.PersonFieldType" }
    )]
    
    [WorkflowTextOrAttribute(
        textLabel: "GroupMe Group Id",
        attributeLabel: "Attribute Value",
        description: "The ID of the GroupMe group to add them to.",
        required: false,
        defaultValue: "",
        category: "",
        order: 3,
        key: AttributeKey.GroupId,
        fieldTypeClassNames: new string[] {
            "Rock.Field.Types.TextFieldType",
            "Rock.Field.Types.IntegerFieldType"
        }
    )]

    [WorkflowAttribute(
        name: "Member Id",
        description: "Optional attribute to hold the member id returned from GroupMe. This id is needed if you want to remove them from the group later.",
        required: false,
        defaultValue: "",
        category: "",
        order: 4,
        key: AttributeKey.MemberId,
        fieldTypeClassNames: new string[] {
            "Rock.Field.Types.TextFieldType",
            "Rock.Field.Types.IntegerFieldType"
        }
    )]
    #endregion Attributes

    public class GroupMeMemberAdd : ActionComponent
    {
        #region Keys

        /// <summary>
        /// Attribute Keys
        /// </summary>
        private static class AttributeKey
        {
            /// <summary>
            /// The person to add
            /// </summary>
            public const string Person = "Person";

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

            Person person = GetPersonFromAttributeValue( _action, AttributeKey.Person, true, _rockContext );
            int? groupId = GetAttributeValue( _action, AttributeKey.GroupId, true ).AsIntegerOrNull();

            var globalAttributeCache = GlobalAttributesCache.Get();
            string accessToken = globalAttributeCache.GetValue( SystemKey.GlobalAttribute.GROUPME_API_TOKEN );

            if ( accessToken.IsNullOrWhiteSpace() )
            {
                errorMessages.Add( "GroupMe Access Token Global Attribute is missing or blank" );
            }
            else if ( person.IsNull() )
            {
                errorMessages.Add( "The Person is required" );
            }
            else if ( groupId.IsNullOrZero() )
            {
                errorMessages.Add( "Invalid GroupMe Group Id." );
            }

            if ( errorMessages.Any() )
            {
                return false;
            }

            var mobilePhoneType = Rock.SystemGuid.DefinedValue.PERSON_PHONE_TYPE_MOBILE.AsGuid();
            var mobileNumber = new PhoneNumberService( rockContext )
                .Queryable()
                .AsNoTracking()
                .Where( p => p.PersonId == person.Id )
                .Where( a => a.NumberTypeValue.Guid == mobilePhoneType )
                .FirstOrDefault();

            if ( mobileNumber.IsNull() )
            {
                errorMessages.Add( "The Person does not have a mobile number." );
                return false;
            }

            var groupMeAPI = new GroupMeAPI( accessToken );
            var group = groupMeAPI.GetGroup( groupId.Value );

            if ( group.IsNull() || group.Name.IsNullOrWhiteSpace() )
            {
                errorMessages.Add( "GroupMe API Error." );
                return false;
            }

            var groupmember = new GroupMeGroupMemberAdd
            {
                Nickname = person.FullName,
                PhoneNumber = mobileNumber.Number
            };

            var added = groupMeAPI.AddGroupMember( group, groupmember, out int memberId );

            if ( !added || memberId == 0 )
            {
                errorMessages.Add( "GroupMe API Error." );
                return false;
            }

            action.AddLogEntry( $"Successfully added { person.FullName } to \"{ group.Name }\" on Groupme." );
            SetWorkflowAttributeValue( _action, AttributeKey.MemberId, memberId );
            
            return true;
        }
    }
}