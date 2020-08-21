/*The MIT License (MIT)

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

// Gets the next available number and adds it to the Target

using System;
using System.Linq;
using Molyom.Constants;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System.Collections.Generic;
using System.Text;

namespace Molyom
{
    public class GetNextUniqueCode : MolyomPlugin
    {
        //
        // This is the main plugin that creates the numbers and adds them to new records
        // This plugin is not registered by default.  It is registered and unregistered dynamically by the CreateAutoNumber and DeleteAutoNumber plugins respectively
        //
        public GetNextUniqueCode(string pluginConfig)
        {
            // Need to support older version
            if (pluginConfig.TryParseJson(out UniqueCodeGeneratorPluginConfig config))
            {
                RegisterEvent(PipelineStage.PreOperation, config.EventName, config.EntityName, Execute);
            }
            else
            {
                RegisterEvent(PipelineStage.PreOperation, PipelineMessage.Create, pluginConfig, Execute);
            }
        }

        protected void Execute(LocalPluginContext context)
        {
            #region Get the list of autonumber records applicable to the target entity type

            var triggerEvent = context.PluginExecutionContext.MessageName;
            var autoNumberIdList = context.OrganizationDataContext.CreateQuery("molyom_uniquecodegenerator")
                                                                  .Where(a => a.GetAttributeValue<string>("molyom_entityname").Equals(context.PluginExecutionContext.PrimaryEntityName) && a.GetAttributeValue<OptionSetValue>("statecode").Value == 0 && a.GetAttributeValue<OptionSetValue>("molyom_triggerevent").Value == (triggerEvent == "Update" ? 1 : 0))
                                                                  .OrderBy(a => a.GetAttributeValue<Guid>("molyom_uniquecodegeneratorid"))  // Insure they are ordered, to prevent deadlocks
                                                                  .Select(a => a.GetAttributeValue<Guid>("molyom_uniquecodegeneratorid"));
            #endregion

            #region This loop locks the autonumber record(s) so only THIS transaction can read/write it

            foreach (var autoNumberId in autoNumberIdList)
            {
                var lockingUpdate = new Entity("molyom_uniquecodegenerator")
                {
                    Id = autoNumberId,
                    ["molyom_preview"] = "555"
                };
                // Use the preview field as our "dummy" field - so we don't need a dedicated "dummy"

                context.OrganizationService.Update(lockingUpdate);
            }

            #endregion

            #region This loop populates the target record, and updates the autonumber record(s)

            if (!(context.PluginExecutionContext.InputParameters["Target"] is Entity target))
            {
                return;
            }

            foreach (var autoNumberId in autoNumberIdList)
            {
                var autoNumber = context.OrganizationService.Retrieve("molyom_uniquecodegenerator", autoNumberId, new ColumnSet(
                    "molyom_attributename",
                    "molyom_triggerattribute",
                    "molyom_conditionaloptionset",
                    "molyom_conditionalvalue",
                    "molyom_characterlength",
                    "molyom_prefix",
                    "molyom_nextcode",
                    "molyom_suffix"));

                var targetAttribute = autoNumber.GetAttributeValue<string>("molyom_attributename");

                #region Check conditions that prevent creating an autonumber

                if (context.PluginExecutionContext.MessageName == "Update" && !target.Contains(autoNumber.GetAttributeValue<string>("molyom_triggerattribute")))
                {
                    continue;  // Continue, if this is an Update event and the target does not contain the trigger value
                }
                else if ((autoNumber.Contains("molyom_conditionaloptionset") && (!target.Contains(autoNumber.GetAttributeValue<string>("molyom_conditionaloptionset")) || target.GetAttributeValue<OptionSetValue>(autoNumber.GetAttributeValue<string>("molyom_conditionaloptionset")).Value != autoNumber.GetAttributeValue<int>("molyom_conditionalvalue"))))
                {
                    continue;  // Continue, if this is a conditional optionset
                }
                else if (target.Contains(targetAttribute) && !string.IsNullOrWhiteSpace(target.GetAttributeValue<string>(targetAttribute)))
                {
                    continue;  // Continue so we don't overwrite a manual value
                }
                else if (triggerEvent == "Update" && context.PreImage.Contains(targetAttribute) && !string.IsNullOrWhiteSpace(context.PreImage.GetAttributeValue<string>(targetAttribute)))
                {
                    context.TracingService.Trace("Target attribute '{0}' is already populated. Continue, so we don't overwrite an existing value.", targetAttribute);
                    continue;  // Continue, so we don't overwrite an existing value
                }

                #endregion

                #region Create the Unique Code

                var preGenerated = false;// autoNumber.GetAttributeValue<bool>("cel_ispregenerated");
                char pad = '0';

                if (preGenerated)  // Pull number from a pre-generated list
                {
                    var preGenNumber = context.OrganizationDataContext.CreateQuery("cel_generatednumber").Where(n => n.GetAttributeValue<EntityReference>("cel_parentautonumberid").Id == autoNumberId && n.GetAttributeValue<OptionSetValue>("statecode").Value == 0).OrderBy(n => n.GetAttributeValue<int>("cel_ordinal")).Take(1).ToList().FirstOrDefault();
                    target[targetAttribute] = preGenNumber["cel_number"] ?? throw new InvalidPluginExecutionException("No available numbers for this record.  Please contact your System Administrator.");

                    var deactivatedNumber = new Entity("cel_generatednumber");
                    deactivatedNumber["statecode"] = new OptionSetValue(1);
                    deactivatedNumber.Id = preGenNumber.Id;

                    context.OrganizationService.Update(deactivatedNumber);
                }
                else  // Do a normal number generation
                {
                    var charLength = autoNumber.GetAttributeValue<int>("molyom_characterlength");
                   
                    var prefix = context.OrganizationService.ReplaceParameters(target, autoNumber.GetAttributeValue<string>("molyom_prefix"));


                    // var number = charLength == 0 ? "" : autoNumber.GetAttributeValue<string>("molyom_nextcode").ToString("D" + charLength);
                    var number = charLength == 0 ? "" : autoNumber.GetAttributeValue<string>("molyom_nextcode").PadLeft(charLength, pad);
                  
                    // string number = autoNumber.GetAttributeValue<string>("molyom_nextcode") == null ? new string('0', charLength) : autoNumber.GetAttributeValue<string>("molyom_nextcode").PadRight(charLength).Substring(0, charLength);

                    var postfix = context.OrganizationService.ReplaceParameters(target, autoNumber.GetAttributeValue<string>("molyom_suffix"));
                    // Generate number and insert into target Record
                    target[targetAttribute] = $"{prefix}{number}{postfix}";

                }

                // Increment next number in db
                var updatedAutoNumber = new Entity("molyom_uniquecodegenerator")
                {
                    Id = autoNumber.Id,
                    // Aqui tengo que incrementar un codigo.
                    ["molyom_nextcode"] = Increment(autoNumber.GetAttributeValue<string>("molyom_nextcode").PadLeft(autoNumber.GetAttributeValue<int>("molyom_characterlength"), pad), Mode.AlphaNumeric),   // + "1",
                    ["molyom_preview"] = target[targetAttribute]
                };

                context.OrganizationService.Update(updatedAutoNumber);

                #endregion
            }
        }

        #endregion


        public enum Mode
        {
            AlphaNumeric = 1,
            Alpha = 2,
            Numeric = 3
        }

        private static string Increment(string text, Mode mode)
        {
            var textArr = text.ToCharArray();

            // Add legal characters
            var characters = new List<char>();

            if (mode == Mode.AlphaNumeric || mode == Mode.Numeric)
                for (char c = '0'; c <= '9'; c++)
                    characters.Add(c);

            if (mode == Mode.AlphaNumeric || mode == Mode.Alpha)
                for (char c = 'a'; c <= 'z'; c++)
                    characters.Add(c);

            // Loop from end to beginning
            for (int i = textArr.Length - 1; i >= 0; i--)
            {
                if (textArr[i] == characters.Last())
                {
                    textArr[i] = characters.First();
                }
                else
                {
                    textArr[i] = characters[characters.IndexOf(textArr[i]) + 1];
                    break;
                }
            }

            return new string(textArr);
        }
    }
}


        // Testing
        //var test1 = Increment("0001", Mode.AlphaNumeric);
        //var test2 = Increment("aab2z", Mode.AlphaNumeric);
        //var test3 = Increment("0009", Mode.Numeric);
        //var test4 = Increment("zz", Mode.Alpha);
        //var test5 = Increment("999", Mode.Numeric);




    

