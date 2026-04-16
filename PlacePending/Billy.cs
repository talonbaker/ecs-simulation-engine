/// Overall:
/// ======================
/// Every two minutes is an hour in-game.
/// You must leave by 8 AM for work.
/// Sleep / wake cycles go in 90 minute.
/// 8 hours of sleep is recommended.
/// Stimulants and other factors help you wake up.
/// Sometimes you can't sleep.
/// There is nothing you NEED to do before you leave.
/// If you don't shower, you'll get smelly. It will begin affecting your day.
/// If you don't eat breakfast, you'll be more tired and it will affect your day.
/// If you don't pack a lunch you will need to eat out, costing money and affecting the other things you can buy for the week.
/// If you don't shave, you'll begin to grow facial hair and it will affect your reputation at work.
/// If you don't wear clean clothing, it will affect your reputation at work.
/// If you're late for work, it will affect your reputation at work.
/// If you don't brush your teeth, it will begin affecting your reputation at work.

/// <summary>
/// This is Billy.
/// Billy is the character the player controls.
/// Billy lives in his apartment and moves from room to room, getting ready in the morning.
/// Billy has two arms with which to grab things,
/// one mouth with which to put food and other things in,
/// two legs with which to walk --- with some connecting bits in between ---
/// and one brain that controlls it all.
/// 
/// Billy is not unique.
/// He, like all of us, gets out of bed every day, puts on his pants one leg at a time, shits, showers, and shaves (if he has time), and goes to work.
/// 
/// He is a meat-puppet,
/// a cog in the corproate machine,
/// a pudgy, skin-covered sack with a tie, a crunchy inner-shell, and sweet nuget deep at the center.
/// 
/// He is a big bowl perpetual, chemical stew garnished with emotions so complex that if he doesn't get at least eight hours of sleep he'll probably cry.
/// </summary>
public class Billy
{
    private Brain _brain;
    private GiTract _giTract;

    //private Arm _armRight;
    //private Arm _armLeft;

    //private Leg _legRight;
    //private Leg _legLeft;

    private bool _isMouthFull = false;
    public bool IsMouthFull
    {
        set { _isMouthFull = value; }
        get { return _isMouthFull; }
    }
 
    #region Constructors
    Billy()
    {
        _brain = new Brain();
        _giTract = new GiTract();
        //_armRight = new Arm();
        //_armLeft = new Arm();
        //_legRight = new Leg();
        //_legLeft = new Leg();
    }
    #endregion

    #region Actions
    /// <summary>
    /// Everything starts with an action.
    /// On a Do request, the brain will be sent an action delegate to be put into memory.
    /// As memories are processed, actions will be preformed in an order which the brain deems most appropriate.
    /// </summary>
    /// <returns>The action to be preformed</returns>
    public Action Do()
    {
        throw new NotImplementedException();
    }
    #endregion
}

/// <summary>
/// Regulated by chemicals beyond our understanding and with complexity to rival spirals at the centers of galixies, the brain is our master. We are but a slave to the brain; if we think otherwise, may we step in mud and invite a flesh-eating parasite to play house in our gray matter.
/// 
/// The brain is a collection of memories and impulses
/// </summary>
public class Brain
{
    /// <summary>
    /// Memory is a priority queue where the priority is based on many factors such as proximity to task, availability of required limbs, or willingness to preform said action at said moment.
    /// 
    /// Emotion and other enviromental factors play a role in what action gets completed at any given time.
    /// 
    /// Priority of action ranges from 0.0 to 1.0 with a 0.0 action never being done (even if it's the only action in the memory queue) and a 1 being done right then, at that exact moment, regardless of ANYTHING ELSE going on (I'm sure you've felt the need to preform a "priority 1.0" action before).
    /// 
    /// Priority is recalculated upon entry of new into memory, new action, new environmental factor, the introduction of any new emotion, and other things.
    /// </summary>
    private PriorityQueue<Action, double> _memory;

    /// <summary>
    /// Setting an action into memory will calculate that action's priority before setting it into priority queue.
    /// Many factors will go into determining an action's priority, all calculated within the brain.
    /// 
    /// An action might also not go into memory for a number of reasons.
    /// In the case of this, method will return false and the queue will not be updated.
    /// </summary>
    /// <param name="action">The action to be preformed</param>
    /// <returns>True if action was set into memory; otherwise, false</returns>
    public bool SetAction(Action action)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Calculating the priority of a single incoming action based on a number of factors.
    /// </summary>
    /// <param name="action">Incoming action to be calculated</param>
    /// <returns>Priority of action as a double between 0.0 and 1.0</returns>
    private double CalculatePriority(Action action)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Recalculation of priorities in memory queue to get an accurate sorting of desired actions based on a number of factors.
    /// </summary>
    private void RecalculatePriorities()
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// The GiTract is a gastrointestinal hellscape complete with a lake of fire, the complete inability to dissolve wads of polyvinyl acetate, and a back door for quick escape when things go south.
/// 
/// Some formulas in metric:
///     33.814 US fluid ounces == 1000 mililiters
///     Conversion formula: oz * 29.574 = ml
/// 
/// Some facts about the GiTract and digestion: generally, food stays in the stomach between 40 minutes to two hours, before spending another 40 minutes to two hours in the small bowel (where it will absorbe most of the nutrients of food). It then spends around five hours in the small intestine, before passing through the colon, which can take anywhere between 10 to 59 hours.
/// </summary>
public class GiTract
{

}

/// <summary>
/// The stomach (AKA: perpetual chemical stew).
/// A roiling, boiling pot that's always cooking. Whatever goes in will be dealt with in some way all while absorbing nutrients and affecting the delicate balance of gut bacteria squirming and worming their way around in a mostly friendly manner.
/// 
/// Processing time in stomach: 40-120 min
/// </summary>
public class Stomach : GiTract
{ 
    public const double MAX_VOLUME = 1500.0; // Volume of average adult GiTract in ml
    private double _currentVolume = 0.0;
}