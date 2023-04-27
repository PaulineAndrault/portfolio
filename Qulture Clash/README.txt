QULTURE CLASH - a multiplayer quiz game
------------------------------------------------------------------------------
A command for a client who organize game nights in a bar. The game master hosts and displays the game on a large TV screen while every team uses a mobile app to play. 
The gameplay is a mix of blind tests and trivia quiz, where players can choose the topics of the game and the difficulty of a question.
Players : 6 to 60 (10 teams max.)
More info in my portfolio here : https://paulineandrault.notion.site/Qulture-Clash-1f0f178360cf4332a98d839fbe3f702c
------------------------------------------------------------------------------
CODE SAMPLES (from the V1 of the game, made in 4 days)

Architecture and game flow : 
- Lobby : straight-to-the-point connection system with manual input of the server's IP address and custom waiting rooms for game master and players.
- GameManager : 
     - Manages the spawn conditions of the server and the clients
     - Reconnection system for teams who might lose their connection during the event (example : death of the smartphone's battery)
     - Manages the transitions between the different game phases (first round of blind test / topics choice / quiz round / events / victory)
- UIManager : manages changes in UI on both server and clients' sides

Core gameplay :
- Question : holds the Question and Answer INetworkSerializable classes and some useful enums
- Team : holds the Team INetworkSerializable class
- QuestionManager : runs the quiz rounds (server and clients sides). Please note that this manager and the BlindTestManager must be refactored due to some redundancies with the UIManager.
- BlindTestManager : runs the blind tests rounds

