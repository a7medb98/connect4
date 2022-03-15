using UnityEngine.UI;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace ConnectFour
{
    public class GameController : MonoBehaviour
    {
        //pvp bool to be set from the modes select in the menu
        public static bool pvp;

        [Range(3, 8)]
        public int numRows = 4;
        [Range(3, 8)]
        public int numColumns = 4;
        [Range(1, 8)]
        public int parallelProcesses = 2;
        [Range(7, 10000)]
        public static int MCTS_Iterations = 1000;

        [Tooltip("Shows column number next to its probability.")]
        public bool log_column = false;

        [Tooltip("How many pieces have to be connected to win.")]
        public int numPiecesToWin = 4;

        [Tooltip("Allow diagonally connected Pieces?")]
        public bool allowDiagonally = true;

        public float dropTime;

        public GameObject pieceRed;
        public GameObject pieceBlue;
        public GameObject pieceField;

        public Text moveTimer;
        private const float timePerMove = 10f;
        private float timeTillMoveComplete;

        public Text playerScores;
        private int playerOneWins;
        private int playerTwoWins;

        public GameObject resultsPanel;
        public Text resultsText;

        private string playerWonText = "You Won!";
        private string playerOneWonText = "Player One Won!";
        private string playerTwoWonText = "Player Two Won!";
        private string playerLoseText = "You Lost!";
        private string drawText = "Draw!";

        GameObject gameObjectField;

        GameObject gameObjectTurn;

        Field field;

        bool isLoading = true;
        bool isDropping = false;
        bool mouseButtonPressed = false;

        bool gameOver = false;
        bool isCheckingForWinner = false;

        void Start()
        {
            timeTillMoveComplete = Time.timeSinceLevelLoad + timePerMove;

            int max = Mathf.Max(numRows, numColumns);

            if (numPiecesToWin > max)
                numPiecesToWin = max;

            CreateField();
        }

        void CreateField()
        {
            isLoading = true;

            gameObjectField = GameObject.Find("Field");
            if (gameObjectField != null)
            {
                DestroyImmediate(gameObjectField);
            }
            gameObjectField = new GameObject("Field");

            field = new Field(numRows, numColumns, numPiecesToWin, allowDiagonally, pvp);

            //if the game is in pvp mode, show the players scores
            if (field.IsPvp)
            {
                playerScores.gameObject.SetActive(true);
                moveTimer.gameObject.SetActive(true);
            }

            if(MCTS_Iterations > 29)
                moveTimer.gameObject.SetActive(true);

            //Load the scores if there are any, else default to show 0
            playerOneWins = PlayerPrefs.GetInt("playerOneWins", 0);
            playerTwoWins = PlayerPrefs.GetInt("playerTwoWins", 0);

            if (!field.IsPvp)
            {
                //display them (\t is tab, leaves a wider space. \n creates a new line)
                playerScores.text = "Player:\t" + playerOneWins + "\nAI:\t\t\t" + playerTwoWins;
            }
            else
                playerScores.text = "Player One:\t" + playerOneWins + "\nPlayer Two:\t" + playerTwoWins;

            for (int x = 0; x < numColumns; x++)
            {
                for (int y = 0; y < numRows; y++)
                {
                    GameObject g = Instantiate(pieceField, new Vector3(x, y * -1, -1), Quaternion.identity);
                    g.transform.parent = gameObjectField.transform;
                }
            }

            isLoading = false;
            gameOver = false;

            Camera.main.transform.position = new Vector3(
              (numColumns - 1) / 2.0f, -((numRows - 1) / 2.0f), Camera.main.transform.position.z);
        }

        GameObject SpawnPiece()
        {
            timeTillMoveComplete = Time.timeSinceLevelLoad + timePerMove;

            Vector3 spawnPos = Camera.main.ScreenToWorldPoint(Input.mousePosition);

            if (!field.IsPlayersTurn)
            {
                int column;
                // Inutile de lancer MCST le premier tour
                if (field.PiecesNumber != 0)
                {
                    // One event is used for each MCTS.
                    ManualResetEvent[] doneEvents = new ManualResetEvent[parallelProcesses];
                    MonteCarloSearchTree[] trees = new MonteCarloSearchTree[parallelProcesses];

                    for (int i = 0; i < parallelProcesses; i++)
                    {
                        doneEvents[i] = new ManualResetEvent(false);
                        trees[i] = new MonteCarloSearchTree(field, doneEvents[i], MCTS_Iterations);
                        ThreadPool.QueueUserWorkItem(new WaitCallback(ExpandTree), trees[i]);
                    }

                    WaitHandle.WaitAll(doneEvents);

                    //regrouping all results
                    Node rootNode = new Node();
                    string log = "";

                    for (int i = 0; i < parallelProcesses; i++)
                    {

                        log += "( ";
                        var sortedChildren = (List<KeyValuePair<Node, int>>)trees[i].rootNode.children.ToList();
                        sortedChildren.Sort((pair1, pair2) => pair1.Value.CompareTo(pair2.Value));

                        foreach (var child in sortedChildren)
                        {

                            if (log_column)
                                log += child.Value + ": ";
                            log += (int)(((double)child.Key.wins / (double)child.Key.plays) * 100) + "% | ";

                            if (!rootNode.children.ContainsValue(child.Value))
                            {
                                Node rootChild = new Node();
                                rootChild.wins = child.Key.wins;
                                rootChild.plays = child.Key.plays;
                                rootNode.children.Add(rootChild, child.Value);
                            }
                            else
                            {
                                Node rootChild = rootNode.children.First(p => p.Value == child.Value).Key;
                                rootChild.wins += child.Key.wins;
                                rootChild.plays += child.Key.plays;
                            }
                        }

                        log = log.Remove(log.Length - 3, 3);
                        log += " )\n";
                    }

                    /****************************/
                    /***** Log final result *****/
                    /****************************/

                    string log2 = "( ";
                    foreach (var child in rootNode.children)
                    {
                        if (log_column)
                            log2 += child.Value + ": ";
                        log2 += (int)(((double)child.Key.wins / (double)child.Key.plays) * 100) + "% | ";
                    }
                    log2 = log2.Remove(log2.Length - 3, 3);
                    log2 += " )\n";
                    log2 += "*********************************************\n";
                    //Debug.Log(log);
                    //Debug.Log(log2);

                    /****************************/

                    column = rootNode.MostSelectedMove();
                }
                else
                    column = field.GetRandomMove();

                spawnPos = new Vector3(column, 0, 0);
            }

            if (!field.IsPvp)
            {
                GameObject g = Instantiate(
                              field.IsPlayersTurn ? pieceBlue : pieceRed,
                              new Vector3(
                                Mathf.Clamp(spawnPos.x, 0, numColumns - 1),
                                gameObjectField.transform.position.y + 1.125f, 0),
                              Quaternion.identity) as GameObject;
                return g;
            }
            else
            {
                GameObject g = Instantiate(
                                field.IsPlayerOnesTurn ? pieceBlue : pieceRed, // is players turn = spawn blue, else spawn red
                                new Vector3(
                                Mathf.Clamp(spawnPos.x, 0, numColumns - 1),
                                gameObjectField.transform.position.y + 1.125f, 0), // spawn it above the first row
                                Quaternion.identity) as GameObject;
                return g;
            }
        }

        public static void ExpandTree(System.Object t)
        {
            var tree = (MonteCarloSearchTree)t;
            tree.simulatedStateField = tree.currentStateField.Clone();
            tree.rootNode = new Node(tree.simulatedStateField.IsPlayersTurn);

            Node selectedNode;
            Node expandedNode;
            System.Random r = new System.Random(System.Guid.NewGuid().GetHashCode());

            for (int i = 0; i < tree.nbIteration; i++)
            {
                tree.simulatedStateField = tree.currentStateField.Clone();

                selectedNode = tree.rootNode.SelectNodeToExpand(tree.rootNode.plays, tree.simulatedStateField);
                expandedNode = selectedNode.Expand(tree.simulatedStateField, r);
                expandedNode.BackPropagate(expandedNode.Simulate(tree.simulatedStateField, r));
            }

            tree.doneEvent.Set();
        }

        public void PlayAgain()
        {
            UnityEngine.SceneManagement.SceneManager.LoadScene(1);
        }

        void Update()
        {
            if (isLoading)
                return;

            if (isCheckingForWinner)
                return;

            if (gameOver)
            {
                resultsPanel.SetActive(true);

                return;
            }

            if(pvp || MCTS_Iterations > 29)
            {
                if (Time.timeSinceLevelLoad < timeTillMoveComplete)
                {
                    moveTimer.text = (timeTillMoveComplete - Time.timeSinceLevelLoad).ToString("F1") + "s";
                }
                else
                {
                    gameOver = true;
                    //if it isnt pvp, tell the player that he either won or lost.
                    if (!field.IsPvp)
                    {
                        resultsText.text = field.IsPlayersTurn ? playerLoseText : playerWonText;

                        if (resultsText.text == playerWonText)
                            playerOneWins++;
                        else
                            playerTwoWins++;

                        PlayerPrefs.SetInt("playerOneWins", playerOneWins);
                        PlayerPrefs.SetInt("playerTwoWins", playerTwoWins);
                    }
                    //if it is pvp, tell the players who won, update and save their scores.
                    else
                    {
                        resultsText.text = field.IsPlayerOnesTurn ? playerTwoWonText : playerOneWonText;
                        if (resultsText.text == playerOneWonText)
                            playerOneWins++;
                        else
                            playerTwoWins++;

                        PlayerPrefs.SetInt("playerOneWins", playerOneWins);
                        PlayerPrefs.SetInt("playerTwoWins", playerTwoWins);
                    }
                }
            }

            if (!field.IsPvp)
            {
                if (field.IsPlayersTurn)
                {
                    if (gameObjectTurn == null)
                    {
                        gameObjectTurn = SpawnPiece();
                    }
                    else
                    {
                        // update the objects position
                        Vector3 pos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
                        gameObjectTurn.transform.position = new Vector3(
                          Mathf.Clamp(pos.x, 0, numColumns - 1),
                          gameObjectField.transform.position.y + 1.125f, 0);

                        // click the left mouse button to drop the piece into the selected column
                        if (Input.GetMouseButtonDown(0) && !mouseButtonPressed && !isDropping)
                        {
                            mouseButtonPressed = true;

                            StartCoroutine(dropPiece(gameObjectTurn));
                        }
                        else
                        {
                            mouseButtonPressed = false;
                        }
                    }
                }
                else
                {
                    if (gameObjectTurn == null)
                    {
                        gameObjectTurn = SpawnPiece();
                    }
                    else
                    {
                        if (!isDropping)
                            StartCoroutine(dropPiece(gameObjectTurn));
                    }
                }
            }
            else
            {
                if (gameObjectTurn == null)
                {
                    gameObjectTurn = SpawnPiece();
                }
                else
                {
                    Vector3 pos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
                    gameObjectTurn.transform.position = new Vector3(
                      Mathf.Clamp(pos.x, 0, numColumns - 1),
                      gameObjectField.transform.position.y + 1.125f, 0);

                    if (Input.GetMouseButtonDown(0) && !mouseButtonPressed && !isDropping)
                    {
                        mouseButtonPressed = true;

                        StartCoroutine(dropPiece(gameObjectTurn));
                    }
                    else
                    {
                        mouseButtonPressed = false;
                    }
                }
            }
        }

        IEnumerator dropPiece(GameObject gObject)
        {
            isDropping = true;

            Vector3 startPosition = gObject.transform.position;
            Vector3 endPosition = new Vector3();

            int x = Mathf.RoundToInt(startPosition.x);
            startPosition = new Vector3(x, startPosition.y, startPosition.z);

            int y = field.DropInColumn(x);

            if (y != -1)
            {
                endPosition = new Vector3(x, y * -1, startPosition.z);

                GameObject g = Instantiate(gObject) as GameObject;
                gameObjectTurn.GetComponent<Renderer>().enabled = false;

                float distance = Vector3.Distance(startPosition, endPosition);

                float t = 0;
                while (t < 1)
                {
                    //dropTime is randomly mutliplied with a number between 0.75f and 1.5f, so that the game doesn't always have the same speed, as it gets repetitive and boring
                    t += Time.deltaTime * (dropTime * Random.Range(0.75f, 1.5f)) * ((numRows - distance) + 1);

                    g.transform.position = Vector3.Lerp(startPosition, endPosition, t);
                    yield return null;
                }

                g.transform.parent = gameObjectField.transform;

                DestroyImmediate(gameObjectTurn);

                StartCoroutine(Won());

                while (isCheckingForWinner)
                    yield return null;

                field.SwitchPlayer();
            }

            isDropping = false;

            yield return 0;
        }

        IEnumerator Won()
        {
            isCheckingForWinner = true;

            gameOver = field.CheckForWinner();

            if (gameOver == true)
            {
                //if it isnt pvp, tell the player that he either won or lost.
                if (!field.IsPvp)
                {
                    resultsText.text = field.IsPlayersTurn ? playerWonText : playerLoseText;

                    if (resultsText.text == playerWonText)
                        playerOneWins++;
                    else
                        playerTwoWins++;

                    PlayerPrefs.SetInt("playerOneWins", playerOneWins);
                    PlayerPrefs.SetInt("playerTwoWins", playerTwoWins);
                }
                //if it is pvp, tell the players who won, update and save their scores.
                else
                {
                    resultsText.text = field.IsPlayerOnesTurn ? playerOneWonText : playerTwoWonText;
                    if (resultsText.text == playerOneWonText)
                        playerOneWins++;
                    else
                        playerTwoWins++;

                    PlayerPrefs.SetInt("playerOneWins", playerOneWins);
                    PlayerPrefs.SetInt("playerTwoWins", playerTwoWins);
                }
            }
            else
            {
                if (!field.ContainsEmptyCell())
                {
                    gameOver = true;
                    resultsText.text = drawText;
                }
            }

            isCheckingForWinner = false;

            yield return 0;
        }

        //Clear the players scores (Called when player quits and when he goes back to main menu)
        public void ClearWins()
        {
            playerOneWins = 0;
            playerTwoWins = 0;

            PlayerPrefs.SetInt("playerOneWins", 0);
            PlayerPrefs.SetInt("playerTwoWins", 0);
        }

        private void OnApplicationQuit() => ClearWins();
    }
}
