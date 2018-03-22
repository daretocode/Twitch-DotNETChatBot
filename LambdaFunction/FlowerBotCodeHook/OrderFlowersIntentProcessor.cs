using Amazon.Lambda.Core;
using Amazon.Lambda.LexEvents;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using static FlowerBotCodeHook.FlowerOrder;

namespace FlowerBotCodeHook
{
    public class OrderFlowersIntentProcessor : AbstractIntentProcessor
    {
        public const string TYPE_SLOT = "FlowerType";
        public const string PICK_UP_DATE_SLOT = "PickUpDate";
        public const string PICK_UP_TIME_SLOT = "PickUpTime";
        public const string INVOCATION_SOURCE = "invocationSource";
        FlowerTypes type = FlowerTypes.Null;

        /// <summary>
        /// Performs dialog management and fulfillment for ordering flowers.
        /// 
        /// Beyond fulfillment, the implementation for this intent demonstrates the following:
        /// 1) Use of elicitSlot in slot validation and re-prompting
        /// 2) Use of sessionAttributes to pass information that can be used to guide the conversation
        /// </summary>
        /// <param name="lexEvent"></param>
        /// <param name="context"></param>
        /// <returns></returns>
        public override LexResponse Process(LexEvent lexEvent, ILambdaContext context)
        {

            IDictionary<string, string> slots = lexEvent.CurrentIntent.Slots;
            IDictionary<string, string> sessionAttributes = lexEvent.SessionAttributes ?? new Dictionary<string, string>();

            if (slots.All(x => x.Value == null))
                { return Delegate(sessionAttributes, slots); }


            if (slots[TYPE_SLOT] != null)
            {
                //Validate the flower type
                Console.WriteLine("FlowerType slot is {0}", slots[TYPE_SLOT]);
                ValidationResult validateFlowerType = ValidateFlowerType(slots[TYPE_SLOT]);

                if (!validateFlowerType.IsValid)
                {
                    slots[validateFlowerType.ViolationSlot] = null;
                    return ElicitSlot(sessionAttributes, 
                            lexEvent.CurrentIntent.Name, 
                            slots, 
                            validateFlowerType.ViolationSlot, 
                            validateFlowerType.Message);
                }
            }

            FlowerOrder order = CreateOrder(slots);
            Console.WriteLine(order.FlowerType.ToString());

            if (string.Equals(lexEvent.InvocationSource, "DialogCodeHook", StringComparison.Ordinal))
            {
                var validateResult = Validate(order);
                // If any slots are invalid, re-elicit for their value
                if (!validateResult.IsValid)
                {
                    slots[validateResult.ViolationSlot] = null;
                    return ElicitSlot(sessionAttributes, lexEvent.CurrentIntent.Name, slots, validateResult.ViolationSlot, validateResult.Message);
                }


                // Pass the price of the flowers back through session attributes to be used in various prompts defined
                // on the bot model.
                if(order.FlowerType.Value != FlowerTypes.Null) {
                    sessionAttributes["Price"] = (order.FlowerType.Value.ToString().Length * 5).ToString();
                }


                return Delegate(sessionAttributes, slots);
            }



            return Close(
                        sessionAttributes,
                        "Fulfilled",
                        new LexResponse.LexMessage
                        {
                            ContentType = MESSAGE_CONTENT_TYPE,
                            Content = String.Format("Thanks, your order for {0} has been placed and will be ready for pickup by {1} on {2}.", order.FlowerType.ToString(), order.PickUpTime, order.PickUpDate)
                        }
                    );
        }

        private ValidationResult ValidateFlowerType(string flowerTypeString)
        {
            bool isFlowerTypeValid = Enum.IsDefined(typeof(FlowerTypes), flowerTypeString.ToUpper());

            if (isFlowerTypeValid)
            {
                Enum.TryParse(flowerTypeString.ToUpper(), out type);
                return ValidationResult.VALID_RESULT;
            }
            else
            {
                return new ValidationResult(false, TYPE_SLOT, 
                    String.Format("We don't have {0} type of flower? Our most popular is roses", flowerTypeString));
            }


        }

            /// <summary>
            /// Verifies that any values for slots in the intent are valid.
            /// </summary>
            /// <param name="order"></param>
            /// <returns></returns>
            private ValidationResult Validate(FlowerOrder order)
        {
            /*if (!order.FlowerType.HasValue && order.FlowerType.Value == FlowerTypes.Null)
            {
                return new ValidationResult(false, TYPE_SLOT,
                    $"We do not have {order.FlowerType}, would you like a different type of flower? Our most popular flowers are roses");
            }*/

            if (!string.IsNullOrEmpty(order.PickUpDate))
            {
                DateTime pickUpDate = DateTime.MinValue;
                if (!DateTime.TryParse(order.PickUpDate, out pickUpDate))
                {
                    return new ValidationResult(false, PICK_UP_DATE_SLOT,
                        "I did not understand that, what date would you like to pick the flowers up?");
                }
                if (pickUpDate < DateTime.Today)
                {
                    return new ValidationResult(false, PICK_UP_DATE_SLOT,
                        "You can pick up the flowers from tomorrow onwards.  What day would you like to pick them up?");
                }
            }

            if (!string.IsNullOrEmpty(order.PickUpTime))
            {
                if(order.PickUpTime.Length != 5)
                {
                    return new ValidationResult(false, PICK_UP_TIME_SLOT, null);
                }
                string[] timeComponents = order.PickUpTime.Split(":");
                Double hour = Double.Parse(timeComponents[0]);
                Double minutes = Double.Parse(timeComponents[1]);

                if(Double.IsNaN(hour) || Double.IsNaN(minutes))
                {
                    return new ValidationResult(false, PICK_UP_TIME_SLOT, null);
                }

                if (hour < 10 || hour >= 17)
                {
                    return new ValidationResult(false, PICK_UP_TIME_SLOT, "Our business hours are from ten a m. to five p m. Can you specify a time during this range?");
                }
          
            }

            return ValidationResult.VALID_RESULT;
        }

        private FlowerOrder CreateOrder(IDictionary<string, string> slots)
        {
            FlowerOrder order = new FlowerOrder
            {
                FlowerType = type,
                PickUpDate = slots.ContainsKey(PICK_UP_DATE_SLOT) ? slots[PICK_UP_DATE_SLOT] : null,
                PickUpTime = slots.ContainsKey(PICK_UP_TIME_SLOT) ? slots[PICK_UP_TIME_SLOT] : null
            };

            return order;

        }


    }

    

}
