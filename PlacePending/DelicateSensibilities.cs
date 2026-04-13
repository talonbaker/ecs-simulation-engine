using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace billygo
{   /// <summary>
    /// Living in a complex world, Billy is affected by his complex emotions (as are we all).
    /// Everything he does, eats, drinks, changes this delicate balance for better or worse.
    /// 
    /// The five main emotions: Joy, Sadness, Anger, Fear, and Disgust --- taken from "Inside Out" --- will help define Billy's fragile sensibilities.
    /// 
    /// The delicate sensibilities class defines how actions and environment affect Billy and his emotional state.
    /// 
    /// The complex emotions help calculate Billy's overall mental state along with other factors.
    /// Each will play a part in determining willingness to do or not do something.
    /// 
    /// Each complex emotion can be helped or hindered by something else. For instance, stress can be avoided at the cost of sleep --- getting up earlier to prepare breakfast, shower, taking time in the morning to get ready.
    /// However, sadness might increase because of it. Or anger.
    /// </summary>
    internal class DelicateSensibilities
    {
        #region Complex Emotions
        /// <summary>
        /// Joy is increased by doing things that are pleasurable: eating warm food, taking hot showers, overall presentable appearance, all playing a roll in determining joyfulness.
        /// </summary>
        private double _joyfulness = 0.0;
        
        /// <summary>
        /// Sadness is something that can't be helped sometimes; doing joyful things will help curb sadness and thus increase overall willingness to do things that might otherwise be harder to do.
        /// </summary>
        private double _sadness= 0.0;

        /// <summary>
        /// Anger can be felt when doing something outside of one's control. If one if made to shower in cold water, wake up before they're ready, do to bed after they're already tired, this will all increase anger and affect other emotions.
        /// </summary>
        private double _anger = 0.0;

        /// <summary>
        /// Fear spikes when the unexpected happens. Putting out fires --- literal or metaphorical --- will increase fear and cause disruptions to the other complex emotions.
        /// </summary>
        private double _fear = 0.0;

        /// <summary>
        /// Disgust, in a more literal sense --- rather than being disgusted at oneself --- will be felt when doing something that makes a person feel like they're doing something they shouldn't be doing. Not showering or brushing teeth, eating undercooked food.
        /// </summary>
        private double _disgust = 0.0;

        /// <summary>
        /// Stress can be helped sometimes; sometimes it can't. Stress can be avoided at the cost of other things.
        /// </summary>
        private double _stress = 0.0;

        /// <summary>
        /// Existential Dread will rise with time. It is only natural.
        /// </summary>
        private double _existentialDread = 0.0;
        #endregion

        #region Energy
        /// <summary>
        /// Energy and motivation are friends; they go hand in hand. In order to function normally, the body must be finely tuned.
        /// This can be hard for a person just trying to get by. Sleep can be had at the cost of time.
        /// </summary>
        private double _sleepiness = 0.0;

        /// <summary>
        /// Motivation can be had when one thinks their actions have meaning. If they always wake up, yet still are late for work every day, one will be less movitated to wake up. If breakfast is always cold, one will be less motivated to eat. Sadness, depression, joy, stress, all play a critical roll in determining a person's motivation to get things done.
        /// </summary>
        private double _motivation = 0.0;
        #endregion

        #region Constructors
        /// <summary>
        /// Presets for base emotions and energies will come from a JSON file later.
        /// </summary>
        public DelicateSensibilities() { }
        #endregion
    }
}
