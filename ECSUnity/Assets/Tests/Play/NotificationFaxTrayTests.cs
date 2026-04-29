using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// AT-09: Fax count increments on order notifications; email indicator blinks.
/// </summary>
[TestFixture]
public class NotificationFaxTrayTests
{
    private GameObject         _go;
    private NotificationPanel  _panel;

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        _go    = new GameObject("NotifFax_Panel");
        _panel = _go.AddComponent<NotificationPanel>();
        yield return null;
    }

    [TearDown]
    public void TearDown()
    {
        foreach (var go in Object.FindObjectsOfType<GameObject>())
            if (go.name.StartsWith("NotifFax_"))
                Object.Destroy(go);
    }

    [UnityTest]
    public IEnumerator Default_FaxCountZero()
    {
        yield return null;
        Assert.AreEqual(0, _panel.FaxCount,
            "Fax count should be zero before any notification is injected.");
    }

    [UnityTest]
    public IEnumerator OneNotification_FaxCountOne()
    {
        _panel.InjectOrderNotification("Fax #1");
        yield return null;

        Assert.AreEqual(1, _panel.FaxCount,
            "Fax count should be 1 after one notification.");
    }

    [UnityTest]
    public IEnumerator TwoNotifications_FaxCountTwo()
    {
        _panel.InjectOrderNotification("Fax #1");
        _panel.InjectOrderNotification("Fax #2");
        yield return null;

        Assert.AreEqual(2, _panel.FaxCount,
            "Fax count should be 2 after two notifications.");
    }

    [UnityTest]
    public IEnumerator EmailBlinking_AfterNotification()
    {
        _panel.InjectOrderNotification("Email #1");
        yield return null;

        Assert.IsTrue(_panel.IsEmailBlinking,
            "Email indicator should be blinking after a notification is injected.");
    }
}
