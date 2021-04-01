using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;


public class Golf : MonoBehaviour {

	static public Golf S;

	[Header("Set in Inspector")]
	public TextAsset deckXML;
	public TextAsset layoutXML;
	public float xOffset = 3;
	public float yOffset = -2.5f;
	public Vector3 layoutCenter;
	public Vector2 fsPosMid = new Vector2(0.5f, 0.90f);
	public Vector2 fsPosRun = new Vector2(0.5f, 0.75f);
	public Vector2 fsPosMid2 = new Vector2(0.4f, 1.0f);
	public Vector2 fsPosEnd = new Vector2(0.5f, 0.95f);
	public float reloadDelay = 2f; // 2 sec delay between rounds
	public Text gameOverText, roundResultText;


	[Header("Set Dynamically")]
	public Deck	deck;
	public Layout layout;
	public List<CardGolf> drawPile;
	public Transform layoutAnchor;
	public CardGolf target;
	public List<CardGolf> tableau;
	public List<CardGolf> discardPile;

	void Awake(){
		S = this;
		SetUpUITexts();
	}

	void SetUpUITexts() {
		// Set up the UI Texts that show at the end of the round
		GameObject go = GameObject.Find("GameOver");
		if (go != null) {
			gameOverText = go.GetComponent<Text>();
		}

		go = GameObject.Find("RoundResult");
		if (go != null) {
			roundResultText = go.GetComponent<Text>();
		}

		// Make the end of round texts invisible
		ShowResultsUI(false);
	}

	void ShowResultsUI(bool show) {
		gameOverText.gameObject.SetActive(show);
		roundResultText.gameObject.SetActive(show);
	}

	void Start() {

		deck = GetComponent<Deck> ();
		deck.InitDeck (deckXML.text);
		Deck.Shuffle(ref deck.cards);
		/* Card c;
		for (int cNum = 0; cNum < deck.cards.Count; cNum++) {
			c = deck.cards[cNum];
			c.transform.localPosition = new Vector3((cNum%13) * 3, cNum/13*4, 0);
		} */

		layout = GetComponent<Layout>(); // Get the Layout component
		layout.ReadLayout(layoutXML.text); // Pass LayoutXML to it

		drawPile = ConvertListCardsToListCardGolfs(deck.cards);
		LayoutGame();
	}

	List<CardGolf> ConvertListCardsToListCardGolfs(List<Card> lCD) {
		List<CardGolf> lCP = new List<CardGolf>();
		CardGolf tCP;
		foreach (Card tCD in lCD) {
			tCP = tCD as CardGolf; // a
			lCP.Add(tCP);
		}
		return (lCP);
	}

	// The Draw function will pull a single card from the drawPile and return it
	CardGolf Draw() {
		CardGolf cd = drawPile[0]; // Pull the 0th CardProspector
		drawPile.RemoveAt(0); // Then remove it from List<> drawPile
		return (cd); // And return it;
	}

	// LayoutGame() positions the initial tableau of cards, a.k.a. the "mine"
	void LayoutGame() {
		// Create an empty GameObject to serve as an anchor for the tableau // a
		if (layoutAnchor == null) {
			GameObject tGO = new GameObject("_LayoutAnchor");
			// ^ Create an empty GameObject named _LayoutAnchor in the Hierarchy
			layoutAnchor = tGO.transform; // Grab its Transform
			layoutAnchor.transform.position = layoutCenter; // Position it
		}

		CardGolf cp;
		// Follow the layout
		foreach (SlotDef tSD in layout.slotDefs) {
			// ^ Iterate through all the SlotDefs in the layout.slotDefs as tSD
			cp = Draw(); // Pull a card from the top (beginning) of the draw Pile
			cp.faceUp = tSD.faceUp; // Set its faceup to the value in SlotDef
			cp.transform.parent = layoutAnchor; // Make its parent layoutAnchor
			// This replaces the previous parent: deck.deckAnchor, which
			// appears as Deck in the Hierarchy when the scene is playing
			cp.transform.localPosition = new Vector3 (
				layout.multiplier.x * tSD.x,
				layout.multiplier.y * tSD.y,
				-tSD.layerID);
			// ^ Set the localPosition of the card based on slotDef
			cp.layoutID = tSD.id;
			cp.slotDef = tSD;
			// CardProspectors in the tableau have the state CardState.tableau
			cp.state = eCardStateGolf.tableau;
			// CardProspectors in the tableau have the state CardState.tableau
			cp.SetSortingLayerName(tSD.layerName); // Set the sorting layers

			tableau.Add(cp); // Add this CardProspector to the List<> tableau
		}

		// Set which cards are hiding others
		foreach (CardGolf tCP in tableau) {
			foreach (int hid in tCP.slotDef.hiddenBy) {
				cp = FindCardByLayoutID(hid);
				tCP.hiddenBy.Add(cp);
			}
		}

		// Set up the initial target card
		MoveToTarget(Draw());

		// Set up the Draw pile
		UpdateDrawPile();
		SetTableauActive();
	}

	// Convert from the layoutID int to the CardProspector with that ID
	CardGolf FindCardByLayoutID(int layoutID) {
		foreach (CardGolf tCP in tableau) {
			// Search through all cards in the tableau List<>
			if (tCP.layoutID == layoutID) {
				// If the card has the same ID, return it
				return(tCP);
			}
		}
		// If it's not found, return null
		return(null);
	}

	void SetTableauFaces() {
		foreach (CardGolf cd in tableau) {
			bool faceUp = true; // Assume the card will be face-up
			foreach (CardGolf cover in cd.hiddenBy) {
				// If either of the covering cards are in the tableau
				if (cover.state == eCardStateGolf.tableau) {
					faceUp = false; // then this card is face-down
				}
			}
			cd.faceUp = faceUp; // Set the value on the card
		}
	}

	void SetTableauActive() {
		foreach (CardGolf cd in tableau) {
			bool isActive = true;
			foreach (CardGolf cover in cd.hiddenBy) {
				if (cover.state == eCardStateGolf.tableau) {
					isActive = false;
				}
			}
			cd.isActive = isActive;
		}
	}

	// Moves the current target to the discardPile
	void MoveToDiscard(CardGolf cd) {
		// Set the state of the card to discard
		cd.state = eCardStateGolf.discard;
		discardPile.Add(cd); // Add it to the discardPile List<>
		cd.transform.parent = layoutAnchor; // Update its transform parent

		// Position this card on the discardPile
		cd.transform.localPosition = new Vector3 (
			layout.multiplier.x * layout.discardPile.x,
			layout.multiplier.y * layout.discardPile.y,
			-layout.discardPile.layerID + 0.5f);
		cd.faceUp = true;
		// Place it on top of the pile for depth sorting
		cd.SetSortingLayerName(layout.discardPile.layerName);
		cd.SetSortOrder(-100 + discardPile.Count);
	}

	// Make cd the new target card
	void MoveToTarget(CardGolf cd) {
		// If there is currently a target card, move it to the discardPile
		if (target != null) MoveToDiscard(target);
		target = cd; // cd is the new target
		cd.state = eCardStateGolf.target;
		cd.transform.parent = layoutAnchor;
		// Move to the target position
		cd.transform.localPosition = new Vector3 (
			layout.multiplier.x * layout.discardPile.x,
			layout.multiplier.y * layout.discardPile.y,
			-layout.discardPile.layerID);
		cd.faceUp = true; // Make it face-up
        cd.isActive = true;

		// Set the depth sorting
		cd.SetSortingLayerName(layout.discardPile.layerName);
		cd.SetSortOrder(0);
	}

	// Arranges all the cards of the drawPile to show how many are left
	void UpdateDrawPile() {
		CardGolf cd;
		// Go through all the cards of the drawPile
		for (int i = 0; i < drawPile.Count; i++) {
			cd = drawPile[i];
			cd.transform.parent = layoutAnchor;

			// Position it correctly with the layout.drawPile.stagger
			Vector2 dpStagger = layout.drawPile.stagger;
			cd.transform.localPosition = new Vector3(
				layout.multiplier.x * (layout.drawPile.x + i * dpStagger.x),
				layout.multiplier.y * (layout.drawPile.y + i * dpStagger.y),
				-layout.drawPile.layerID + 0.1f * i);
			cd.faceUp = false; // Make them all face-down
			cd.state = eCardStateGolf.drawpile;
			// Set depth sorting
			cd.SetSortingLayerName(layout.drawPile.layerName);
			cd.SetSortOrder(-10 * i);
		}
	}

	// CardClicked is called any time a card in the game is clicked
	public void CardClicked(CardGolf cd) {
		// The reaction is determined by the state of the clicked card
		switch (cd.state) {
			case eCardStateGolf.target:
				// Clicking the target card does nothing
				break;
			case eCardStateGolf.drawpile:
				// Clicking any card in the drawPile will draw the next card
				MoveToDiscard(target); // Moves the target to the discardPile
				MoveToTarget(Draw()); // Moves the next drawn card to the target
				UpdateDrawPile(); // Restacks the drawPile
				break;
			case eCardStateGolf.tableau:
				// Clicking a card in the tableau will check if it's a valid play
				bool validMatch = true;
				if (!cd.faceUp) {
					// If the card is face-down, it's not valid
					validMatch = false;
				}
				if (!cd.isActive) {
					validMatch = false;
				}
				if (!AdjacentRank(cd, target)) {
					// If it's not an adjacent rank, it's not valid
					validMatch = false;
				}
				if (!validMatch) return; // return if not valid

				// If we got here, then: Yay! it's a valid card
				tableau.Remove(cd); // Remove it from the tableau List
				MoveToTarget(cd); // Make it the target card
				SetTableauFaces(); // Update the tableau card face-ups
				SetTableauActive();
				break;
		}
		// Check to see if whether the game is over or not
		CheckForGameOver();
	}

	// Test whether the game is over
	void CheckForGameOver() {
		// If tableau is empty, the game is over
		if (tableau.Count == 0) {
			// Call GameOver() with a win
			GameOver(true);
			return;
		}

		// If there are still cards in the draw pile, the game's not over
		if (drawPile.Count > 0) {
			return;
		}

		// Check for remaining valid plays
		foreach (CardGolf cd in tableau) {
			if (AdjacentRank(cd, target)) {
				// If there is a valid play, the game's not over
				return;
			}
		}

		// Since there are no valid plays, the game is over
		// Call GameOver with a loss
		GameOver(false);
	}

	// Called when the game is over. Simple for now, but expandable
	void GameOver(bool won) {
		int score = 0;
        bool emptyTab = true;

        foreach (CardGolf cd in tableau) {
            score++;
            emptyTab = false;
        }

        if (score == 0 && emptyTab) {
            foreach (CardGolf cd in drawPile) {
                score--;
            }
        }

		if (won) {
			gameOverText.text = "Round Over";
			roundResultText.text = "You won this round!\nScore: " + score;
			ShowResultsUI(true);
			// print("Game Over. You won! :)");
		} else {
			gameOverText.text = "Game Over";
            roundResultText.text = "You lost this round\nScore: " + score;
			ShowResultsUI(true);
			// print("Game Over. You lost. :(");
		}
		// Reload the scene, resetting the game
		// SceneManager.LoadScene("__Prospector_Scene_0");

		// Reload the scene in reloadDelay seconds
		// This will give the score a moment to travel
		Invoke("ReloadLevel", reloadDelay);
	}

	void ReloadLevel() {
		// Reload the scene, resetting the game
		SceneManager.LoadScene("__Golf_Solitaire_Scene");
	}

	// Return true if the two cards are adjacent in rank (A & K wrap around)
	public bool AdjacentRank(CardGolf c0, CardGolf c1) {
		// If either card is face-down, it's not-adjacent
		if (!c0.isActive || !c1.isActive) return(false);

		// If they are 1 apart, they are adjacent
		if (Mathf.Abs(c0.rank - c1.rank) == 1) {
			return(true);
		}
		// If one is Ace and the other King, they are adjacent
		if (c0.rank == 1 && c1.rank == 13) return(true);
		if (c0.rank == 13 && c1.rank == 1) return(true);

		//Otherwise, return false
		return(false);
	}

}
