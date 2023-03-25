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

namespace com.valorouschurch.WorkflowActions.Workflow.Action.People
{
    /// <summary>
    /// Creates a new UserLogin for the Person using the Database authentication type.
    /// </summary>
    [ActionCategory( "Valorous > People" )]
    [Description( "Creates a new UserLogin for the Person using the Database authentication type." )]
    [Export( typeof( ActionComponent ) )]
    [ExportMetadata( "ComponentName", "Create Database Login" )]

    [WorkflowAttribute( "Person",
        Description = "Workflow attribute that contains the person to add the login to.",
        IsRequired = true,
        Key = AttributeKey.Person,
        FieldTypeClassNames = new string[] { "Rock.Field.Types.PersonFieldType" },
        Order = 1 )]

    [WorkflowTextOrAttribute( "Username", "Attribute Value",
        Description = "The Username to create",
        IsRequired = true,
        Key = AttributeKey.Username,
        FieldTypeClassNames = new string[] { "Rock.Field.Types.TextFieldType" },
        Order = 2 )]

    [WorkflowTextOrAttribute( "Password", "Attribute Value",
        Description = "The password to use",
        IsRequired = true,
        Key = AttributeKey.Password,
        FieldTypeClassNames = new string[] { "Rock.Field.Types.TextFieldType" },
        Order = 3 )]

    [BooleanField( "Must Change",
        Description = "Should the user be required to change their password on the first login?",
        IsRequired = true,
        Key = AttributeKey.MustChange,
        Order = 4 )]
    public class CreateDatabaseLogin : ActionComponent
    {
        private class AttributeKey
        {
            public const string Person = "Person";
            public const string Username = "Username";
            public const string Password = "Password";
            public const string MustChange = "MustChange";
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

            // Get the Person entity from Attribute.
            PersonAlias personAlias = null;
            string personAttributeValue = GetAttributeValue( action, AttributeKey.Person );
            Guid? guidPersonAttribute = personAttributeValue.AsGuidOrNull();
            if ( guidPersonAttribute.HasValue )
            {
                var attributePerson = AttributeCache.Get( guidPersonAttribute.Value, rockContext );
                if ( attributePerson != null && attributePerson.FieldType.Class == "Rock.Field.Types.PersonFieldType" )
                {
                    Guid? attributePersonValue = action.GetWorkflowAttributeValue( guidPersonAttribute.Value ).AsGuidOrNull();
                    if ( attributePersonValue.HasValue )
                    {
                        personAlias = new PersonAliasService( rockContext ).Queryable()
                            .Where( a => a.Guid.Equals( attributePersonValue.Value ) )
                            .FirstOrDefault();
                    }
                }
            }

            if ( personAlias == null )
            {
                errorMessages.Add( string.Format( "Person could not be found for selected value ('{0}')!", guidPersonAttribute.ToString() ) );
                return false;
            }

            var username = GetAttributeValue( action, AttributeKey.Username, true );
            var password = GetAttributeValue( action, AttributeKey.Password, true );
            var mustChange = GetAttributeValue( action, AttributeKey.MustChange, true ).AsBoolean();

            try
            {
                UserLoginService.Create(
                    rockContext,
                    personAlias.Person,
                    AuthenticationServiceType.Internal,
                    EntityTypeCache.Get( Rock.SystemGuid.EntityType.AUTHENTICATION_DATABASE.AsGuid() ).Id,
                    username,
                    password,
                    true,
                    mustChange );
            }
            catch ( ArgumentOutOfRangeException e )
            {
                // Thrown when the username already exists
                if ( e.ParamName == "username" )
                {
                    errorMessages.Add( string.Format( "The username {1} already exists.", username ) );
                    return false;
                }
                else
                {
                    throw e;
                }
            }
            catch ( ArgumentException e )
            {
                // Thrown when authentication service "database" is inactive
                errorMessages.Add( e.Message );
                return false;
            }

            return true;
        }
    }
}