﻿/*The MIT License (MIT)

Copyright (c) 2020 Molyom

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
*/

// Generates a plugin step for an entity, when a new Unique Code record is created

using System;
using System.Linq;
using Microsoft.Xrm.Sdk;
using Molyom.Constants;

namespace Molyom
{
	public class CreateUniqueCode : MolyomPlugin
	{
		//
		// This plugin is executed when a new Unique Code record is created.  It generates the plugin steps on the entity type to create each Code
		//
		// Registration Details:
		// Message: Create
		// Primary Entity: molyom_uniquecodegenerator
		// User context: SYSTEM
		// Event Pipeline: Post
		// Mode: Async
		// Config: none
		//

		internal const string PluginName = "Molyom.UniqueCodeGenerator.{0}";

		public CreateUniqueCode()
		{
			RegisterEvent(PipelineStage.PostOperation, PipelineMessage.Create, "molyom_uniquecodegenerator", Execute);
		}

		protected void Execute(LocalPluginContext context)
		{
		    context.Trace("Get Target record");
			var target = context.GetInputParameters<CreateInputParameters>().Target;
			var pluginName = string.Format(PluginName, target.GetAttributeValue<string>("molyom_entityname"));

			if (target.GetAttributeValue<OptionSetValue>("molyom_triggerevent").Value == 1)
			{
				pluginName += " Update";
			}

		    context.Trace("Check for existing plugin step");
			if (context.OrganizationDataContext.CreateQuery("sdkmessageprocessingstep").Where(s => s.GetAttributeValue<string>("name").Equals(pluginName)).ToList().Any())
			{
				return;  // Step already exists, nothing to do here.
			}

		    context.Trace("Build the configuration");
			var config = new UniqueCodeGeneratorPluginConfig()
			{
				EntityName = target.GetAttributeValue<string>("molyom_entityname"),
				EventName = target.GetAttributeValue<OptionSetValue>("molyom_triggerevent").Value == 1 ? "Update" : "Create"
			};

		    context.Trace("Get the Id of this plugin");
		    var pluginTypeId = context.OrganizationDataContext.CreateQuery("plugintype")
				 											   .Where(s => s.GetAttributeValue<string>("name").Equals(typeof(GetNextUniqueCode).FullName))
															   .Select(s => s.GetAttributeValue<Guid>("plugintypeid"))
															   .First();

		    context.Trace("Get the message id from this org");
		    var messageId = context.OrganizationDataContext.CreateQuery("sdkmessage")  
															.Where(s => s.GetAttributeValue<string>("name").Equals(config.EventName))
															.Select(s => s.GetAttributeValue<Guid>("sdkmessageid"))
															.First();

		    context.Trace("Get the filterId for for the specific entity from this org");
			var filterId = context.OrganizationDataContext.CreateQuery("sdkmessagefilter")  
														   .Where(s => s.GetAttributeValue<string>("primaryobjecttypecode").Equals(config.EntityName)
															   && s.GetAttributeValue<EntityReference>("sdkmessageid").Id.Equals(messageId))
														   .Select(s => s.GetAttributeValue<Guid>("sdkmessagefilterid"))
														   .First();

		    context.Trace("Build new plugin step");
			var newPluginStep = new Entity("sdkmessageprocessingstep")
			{
				Attributes = new AttributeCollection()
				{
					{ "name", pluginName },
					{ "description", pluginName },
					{ "plugintypeid", pluginTypeId.ToEntityReference("plugintype") },  // This plugin type
					{ "sdkmessageid", messageId.ToEntityReference("sdkmessage") },  // Create or Update Message
					{ "configuration", config.ToJson() },  // EntityName and RegisteredEvent in the UnsecureConfig
					{ "stage", PipelineStage.PreOperation.ToOptionSetValue() },  // Execution Stage: Pre-Operation
					{ "rank", 1 },
					{ "impersonatinguserid", context.PluginExecutionContext.UserId.ToEntityReference("systemuser") },  // Run as SYSTEM user. Assumes we are currently running as the SYSTEM user
					{ "sdkmessagefilterid", filterId.ToEntityReference("sdkmessagefilter") },
				}
			};

		    context.Trace("Create new plugin step");
		    var sdkmessageprocessingstepid = context.OrganizationService.Create(newPluginStep);

            // only add the image if the type is update, on create a value cannot be overridden
		    if (target.GetAttributeValue<OptionSetValue>("molyom_triggerevent").Value == 1)
		    {
		        context.Trace("Build new plugin step image");
		        var newPluginStepImage = new Entity("sdkmessageprocessingstepimage")
		        {
		            Attributes = new AttributeCollection()
		            {
		                {"sdkmessageprocessingstepid", sdkmessageprocessingstepid.ToEntityReference("sdkmessageprocessingstep")},
		                {"imagetype", 0.ToOptionSetValue()}, // PreImage
		                {"messagepropertyname", "Target"},
		                {"name", "Image"}, 
		                {"entityalias", "Image"}, 
		                {"attributes", target.GetAttributeValue<string>("molyom_attributename")}, //Only incluce the one attribute we really need. 
		            }
		        };

		        context.Trace("Create new plugin step image");
		        context.OrganizationService.Create(newPluginStepImage);
		    }
		}
	}
}
