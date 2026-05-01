using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// AT-09: Notification panel phone ring on new order injection.
/// </summary>
[TestFixture]
public class NotificationPhoneRingTests
{
    private GameObject         _go;
    private NotificationPanel  _panel;

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        _go    = new GameObject("NotifPhone_Panel");
        _panel = _go.AddComponent<NotificationPanel>();
        yield return null;
    }

    [TearDown]
    public void TearDown()
    {
        foreach (var go in Object.FindObjectsOfType<GameObject>())
            if (go.name.StartsWith("NotifPhone_"))
                Object.Destroy(go);
    }

    [UnityTest]
    public IEnumerator Default_PhoneNotRinging()
    {
        yield return null;
        Assert.IsFalse(_panel.IsPhoneRinging,
            "Phone should not be ringing before any notification is injected.");
    }

    [UnityTest]
    public IEnumerator InjectOrderNotification_PhoneRings()
    {
        _panel.InjectOrderNotification("Order #1");
        yield return null;

        Assert.IsTrue(_panel.IsPhoneRinging,
            "Phone should ring after an order notification is injected.");
    }

    [UnityTest]
    public IEnumerator MultipleOrders_PhoneStillRinging()
    {
        _panel.InjectOrderNotification("Order #1");
        _panel.InjectOrderNotification("Order #2");
        yield return null;

        Assert.IsTrue(_panel.IsPhoneRinging,
            "Phone should still be ringing after multiple order notifications.");
    }

    [UnityTest]
    public IEnumerator AfterAcknowledge_PhoneStops()
    {
        _panel.InjectOrderNotification("Order #1");
        yield return null;
        Assert.IsTrue(_panel.IsPhoneRinging);

        _panel.AcknowledgePhone();
        yield return null;

        Assert.IsFalse(_panel.IsPhoneRinging,
            "Phone should stop ringing after AcknowledgePhone() is called.");
    }
}
