/*
Original Source:
  - https://github.com/ShepherdDev/workflow-actions/blob/2a3c1ba7f2b43f56401272cbd1e5960b787a219a/SetAttributeToRoundRobinGroupMember.cs
*/

using System;
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

namespace com.valorouschurch.WorkflowActions.Workflow.Action.Groups
{
    /// <summary>
    /// Sets an attribute to the Person of a group using a round-robin selection style.
    /// </summary>
    [ActionCategory( "Valorous > Groups" )]
    [Description( "Sets an attribute to the Person of a group using a round-robin selection style." )]
    [Export( typeof( ActionComponent ) )]
    [ExportMetadata( "ComponentName", "Set Attribute To Round Robin Group Member" )]

    [GroupField( "Group",
        Description = "Specific group for selecting a person from.",
        IsRequired = false,
        DefaultValue ="",
        Order = 1,
        Key = AttributeKey.GroupExplicit )]

    [WorkflowAttribute( "Group Attribute",
        Description ="Workflow Attribute that contains the group for selecting a person from.",
        IsRequired = false,
        Order = 2,
        Key = AttributeKey.GroupAttribute,
        FieldTypeClassNames = new string[] { "Rock.Field.Types.GroupFieldType" } )]

    [EnumsField( "Group Member Status",
        Description ="Group member must be one of these status to match.",
        EnumSourceType = typeof( GroupMemberStatus ),
        IsRequired = true,
        DefaultValue = "1,2",
        Order = 3 )]

    // GroupRoleField doesn't support the new format for attributes...
    [GroupRoleField( name: "Filter by Group Type Role",
        groupTypeGuid: "",
        description: "The role the person must have in the group to be considered as valid membership. By default all Group Members are considered.",
        required: false,
        order: 4,
        key: "GroupRole" )]

    [WorkflowAttribute( "Attribute",
        Description = "The attribute to set with the Person picked from the group.",
        IsRequired = true,
        Order = 5,
        FieldTypeClassNames = new string[] { "Rock.Field.Types.PersonFieldType", "Rock.Field.Types.TextFieldType" } )]

    public class SetAttributeToRoundRobinGroupMember : ActionComponent
    {
        private class AttributeKey
        {
            public const string GroupExplicit = "GroupExplicit";
            public const string GroupAttribute = "GroupAttribute";
            public const string GroupMemberStatus = "GroupMemberStatus";
            public const string GroupRole = "GroupRole";
            public const string Attribute = "Attribute";
        }

        private readonly object _lockObject = new object();

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
            Group group = null;
            Person person = null;

            // Get the selected group from the configuration options.
            var groupGuid = GetAttributeValue( action, AttributeKey.GroupExplicit ).AsGuidOrNull();
            if ( !groupGuid.HasValue )
            {
                var guidGroupAttribute = GetAttributeValue( action, AttributeKey.GroupAttribute ).AsGuidOrNull();
                if ( guidGroupAttribute.HasValue )
                {
                    var attributeGroup = AttributeCache.Get( guidGroupAttribute.Value, rockContext );
                    if ( attributeGroup != null )
                    {
                        groupGuid = action.GetWorkflowAttributeValue( guidGroupAttribute.Value ).AsGuidOrNull();
                    }
                }
            }

            if ( groupGuid.HasValue )
            {
                group = new GroupService( rockContext ).Get( groupGuid.Value );
            }

            if ( group == null )
            {
                errorMessages.Add( "No group was provided" );
            }
            else
            {
                // Get the next person to be picked.
                person = GetNextPerson( action, group.Id );

                if ( person == null )
                {
                    errorMessages.Add( "Group contains no valid members" );
                }
            }

            // Set the attribute value.
            if ( !errorMessages.Any() )
            {
                // Set value of the selected attribute.
                Guid selectAttributeGuid = GetAttributeValue( action, AttributeKey.Attribute ).AsGuid();
                if ( !selectAttributeGuid.IsEmpty() )
                {
                    var selectedPersonAttribute = AttributeCache.Get( selectAttributeGuid, rockContext );
                    if ( selectedPersonAttribute != null )
                    {
                        if ( selectedPersonAttribute.FieldTypeId == FieldTypeCache.Get( Rock.SystemGuid.FieldType.TEXT.AsGuid(), rockContext ).Id )
                        {
                            SetWorkflowAttributeValue( action, selectAttributeGuid, person.FullName );
                        }
                        else
                        {
                            SetWorkflowAttributeValue( action, selectAttributeGuid, person.PrimaryAlias.Guid.ToString() );
                        }
                    }
                }
            }

            errorMessages.ForEach( m => action.AddLogEntry( m, true ) );

            return true;
        }

        /// <summary>
        /// Get the next person for the action and group.
        /// </summary>
        /// <param name="action">The action that needs the next round robin person picked.</param>
        /// <param name="groupId">The identifier of the group to load the next person from.</param>
        /// <returns>A person object that represents the next person in the round robin selection process.</returns>
        private Person GetNextPerson( WorkflowAction action, int groupId )
        {
            Person person = null;

            // Lock so that only one thread can do this at a time. This is to ensure that
            // we don't have two workflows processing at the same time and trying to get
            // the next person in the round robin and overwriting each others changes.
            lock ( _lockObject )
            {
                var attrKey = action.ActionTypeCache.Guid.ToString();
                // Load attributes if we need to.
                if ( action.Activity.Workflow.Attributes == null )
                {
                    action.Activity.Workflow.LoadAttributes();
                }

                // Check if we already have an attribute.
                if ( !action.Activity.Workflow.Attributes.ContainsKey( attrKey ) )
                {
                    var newAttribute = new Rock.Model.Attribute
                    {
                        EntityTypeId = action.Activity.Workflow.TypeId,
                        EntityTypeQualifierColumn = "WorkflowTypeId",
                        EntityTypeQualifierValue = action.Activity.Workflow.WorkflowTypeId.ToString(),
                        Name = string.Format( "Last Round Robin Person ({0})", action.ActionTypeCache.Name ),
                        Key = attrKey,
                        FieldTypeId = FieldTypeCache.Get( Rock.SystemGuid.FieldType.TEXT.AsGuid() ).Id
                    };

                    using ( var newRockContext = new RockContext() )
                    {
                        new AttributeService( newRockContext ).Add( newAttribute );
                        newRockContext.SaveChanges();
                        EntityTypeAttributesCache.FlushItem( action.Activity.Workflow.TypeId );
                    }

                    action.Activity.Workflow.Attributes.Add( attrKey, AttributeCache.Get( newAttribute ) );
                }

                // Get the last person selected.
                var lastPersonId = action.Activity.Workflow.GetAttributeValue( attrKey ).AsIntegerOrNull();

                var rockContext = new RockContext();

                // Get the list of group members to pick from.
                var groupMemberService = new GroupMemberService( rockContext );
                var statuses = this.GetAttributeValue( action, AttributeKey.GroupMemberStatus )
                    .SplitDelimitedValues()
                    .Select( s => ( GroupMemberStatus ) Enum.Parse( typeof( GroupMemberStatus ), s ) )
                    .ToList();
                var groupMembers = groupMemberService.Queryable()
                    .Where( m => m.GroupId == groupId && statuses.Contains( m.GroupMemberStatus ) )
                    .DistinctBy( m => m.PersonId )
                    .OrderBy( m => m.Person.LastName )
                    .ThenBy( m => m.Person.FirstName )
                    .ToList();

                // Filter by the group role.
                if ( !string.IsNullOrWhiteSpace( GetAttributeValue( action, AttributeKey.GroupRole ) ) )
                {
                    var groupRole = new GroupTypeRoleService( rockContext ).Get( GetAttributeValue( action, AttributeKey.GroupRole ).AsGuid() );

                    groupMembers = groupMembers.Where( m => m.GroupRoleId == groupRole.Id ).ToList();
                }

                // Find the next person.
                if ( groupMembers.Count > 0 )
                {
                    int lastMemberIndex = -1;

                    // Find the index of the last person picked.
                    if ( lastPersonId.HasValue )
                    {
                        var lastMember = groupMembers
                            .Where( m => !lastPersonId.HasValue || m.PersonId == lastPersonId )
                            .FirstOrDefault();
                        if ( lastMember != null )
                        {
                            lastMemberIndex = groupMembers.IndexOf( lastMember );
                        }
                    }

                    // Pick either the next or the first person.
                    if ( lastMemberIndex < 0 || ( lastMemberIndex + 1 ) >= groupMembers.Count )
                    {
                        person = groupMembers.First().Person;
                    }
                    else
                    {
                        person = groupMembers[lastMemberIndex + 1].Person;
                    }
                }

                // Update the attribute value.
                var attributeService = new AttributeService( rockContext );
                var attribute = attributeService.Get( action.Activity.Workflow.TypeId, "WorkflowTypeId", action.Activity.Workflow.WorkflowTypeId.ToString(), attrKey );
                attribute.DefaultValue = person.Id.ToString();
                rockContext.SaveChanges();
            }

            return person;
        }
    }
}