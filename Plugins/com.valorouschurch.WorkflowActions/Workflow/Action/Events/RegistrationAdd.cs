﻿// <copyright>
// Copyright Pillars Inc.
// </copyright>
//

/*
Original Source:
  - https://github.com/PillarsForRock/CustomWorkflowActions/blob/15c164b16c8ae778679a41a6e41b00189f0c3b4f/rocks.pillars.WorkflowActions/Workflow/Action/Events/RegistrationAdd.cs
*/

using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.Composition;
using System.Linq;

using Rock;
using Rock.Attribute;
using Rock.Data;
using Rock.Model;
using Rock.Workflow;

namespace com.valorouschurch.WorkflowActions.Workflow.Action.Events
{
    /// <summary>
    /// Sets an attribute's value to the selected person 
    /// </summary>
    [ActionCategory( "Valorous > Events" )]
    [Description( "Adds a registration and registrar to a specific event instance, and returns the registration id." )]
    [Export( typeof( ActionComponent ) )]
    [ExportMetadata( "ComponentName", "Registration Add" )]

    [WorkflowTextOrAttribute( "Registration Instance ID",
        "Attribute Value",
        Description ="Registration instance that the registration should be added to. <span class='tip tip-lava'></span>",
        IsRequired = true,
        DefaultValue = "",
        Category = "",
        Order = 1,
        Key = AttributeKey.RegistrationInstanceId,
        FieldTypeClassNames = new string[] { "Rock.Field.Types.IntegerFieldType" } )]

    [WorkflowAttribute( "Registrar",
        Description = "Workflow attribute that contains the person to add as the registrar.",
        IsRequired = true,
        DefaultValue = "",
        Category = "",
        Order = 2,
        Key = AttributeKey.Registrar,
        FieldTypeClassNames = new string[] { "Rock.Field.Types.PersonFieldType" } )]

    [WorkflowAttribute( "Result Attribute",
        Description = "An optional attribute to set to the registration id that is created.",
        IsRequired = false,
        DefaultValue = "",
        Category = "",
        Order = 3,
        Key = AttributeKey.ResultAttribute )]

    public class RegistrationAdd : ActionComponent
    {
        private static class AttributeKey
        {
            public const string RegistrationInstanceId = "RegistrationInstanceId";
            public const string Registrar = "Registrar";
            public const string ResultAttribute = "ResultAttribute";
        }

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

            // get the registration instance
            RegistrationInstance instance = new RegistrationInstanceService( rockContext ).Get( GetAttributeValue( action, AttributeKey.RegistrationInstanceId, true ).AsInteger() );
            if ( instance == null )
            {
                errorMessages.Add( "The Registration Instance could not be determined or found!" );
            }

            // determine the person that will be added to the registration instance
            Person person = null;
            var personAliasGuid = GetAttributeValue( action, AttributeKey.Registrar, true ).AsGuidOrNull();
            if ( personAliasGuid.HasValue )
            {
                person = new PersonAliasService( rockContext ).Queryable()
                    .Where( a => a.Guid.Equals( personAliasGuid.Value ) )
                    .Select( a => a.Person )
                    .FirstOrDefault();
            }

            if ( person == null || !person.PrimaryAliasId.HasValue )
            {
                errorMessages.Add( "The Person for the Registrar value could not be determined or found!" );
            }

            // Add registration
            if ( !errorMessages.Any() )
            {
                var registrationService = new RegistrationService( rockContext );

                var registration = new Registration();
                registrationService.Add( registration );
                registration.RegistrationInstanceId = instance.Id;
                registration.PersonAliasId = person.PrimaryAliasId.Value;
                registration.FirstName = person.NickName;
                registration.LastName = person.LastName;
                registration.IsTemporary = false;
                registration.ConfirmationEmail = person.Email;

                rockContext.SaveChanges();

                if ( registration.Id > 0 )
                {
                    string resultValue = registration.Id.ToString();
                    var attribute = SetWorkflowAttributeValue( action, AttributeKey.ResultAttribute, resultValue );
                    if ( attribute != null )
                    {
                        action.AddLogEntry( string.Format( "Set '{0}' attribute to '{1}'.", attribute.Name, resultValue ) );
                    }
                }
            }

            errorMessages.ForEach( m => action.AddLogEntry( m, true ) );

            return !errorMessages.Any();
        }
    }
}