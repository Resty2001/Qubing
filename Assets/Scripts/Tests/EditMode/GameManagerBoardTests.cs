using NUnit.Framework;
using UnityEngine;

public sealed class GameManagerBoardTests
{
    private GameObject gameManagerObject;
    private GameManager gameManager;

    [SetUp]
    public void SetUp()
    {
        gameManagerObject = new GameObject("GameManager_Test");
        gameManager = gameManagerObject.AddComponent<GameManager>();
    }

    [TearDown]
    public void TearDown()
    {
        if (gameManagerObject != null)
        {
            Object.DestroyImmediate(gameManagerObject);
        }
    }

    [TestCase(0, 0, true)]
    [TestCase(3, 3, true)]
    [TestCase(6, 6, true)]
    [TestCase(-1, 0, false)]
    [TestCase(0, -1, false)]
    [TestCase(7, 0, false)]
    [TestCase(0, 7, false)]
    public void IsInsideBoard_ReturnsExpectedResult(
        int x,
        int y,
        bool expected)
    {
        bool result = gameManager.IsInsideBoard(new Vector2Int(x, y));

        Assert.That(result, Is.EqualTo(expected));
    }
}