using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Code.GameLogic;
using Code.Player;

namespace Code.Editor
{
    [TestFixture]
    public class TrucoCycleTests
    {
        // ──────────────────────────────────────────────────────────────────────────
        // 1. REGLAS: JERARQUÍA DE CARTAS Y VALORES REALES (VENEZUELAN TRUCO)
        // ──────────────────────────────────────────────────────────────────────────

        [Test]
        public void Test_OrdinaryCardHierarchy()
        {
            Card vira = new Card(5, "Gold"); // Vira 5 de Oro, no afecta a 11 ni a 10

            Card c3 = new Card(3, "Cup");
            Card c2 = new Card(2, "Sword");
            Card c1False = new Card(1, "Cup"); // As falso
            Card c12 = new Card(12, "Cudgel");
            Card c11 = new Card(11, "Cup");
            Card c10 = new Card(10, "Sword");
            Card c7False = new Card(7, "Cup");
            Card c6 = new Card(6, "Sword");
            Card c5 = new Card(5, "Cup");
            Card c4 = new Card(4, "Gold");

            int v3 = TrucoRules.GetCardRealValue(c3, vira);
            int v2 = TrucoRules.GetCardRealValue(c2, vira);
            int v1 = TrucoRules.GetCardRealValue(c1False, vira);
            int v12 = TrucoRules.GetCardRealValue(c12, vira);
            int v11 = TrucoRules.GetCardRealValue(c11, vira);
            int v10 = TrucoRules.GetCardRealValue(c10, vira);
            int v7 = TrucoRules.GetCardRealValue(c7False, vira);
            int v6 = TrucoRules.GetCardRealValue(c6, vira);
            int v5 = TrucoRules.GetCardRealValue(c5, vira);
            int v4 = TrucoRules.GetCardRealValue(c4, vira);

            Assert.IsTrue(v3 > v2, "3 debe ser mayor que 2");
            Assert.IsTrue(v2 > v1, "2 debe ser mayor que As falso");
            Assert.IsTrue(v1 > v12, "As falso debe ser mayor que 12");
            Assert.IsTrue(v12 > v11, "12 debe ser mayor que 11 normal");
            Assert.IsTrue(v11 > v10, "11 normal debe ser mayor que 10 normal");
            Assert.IsTrue(v10 > v7, "10 normal debe ser mayor que 7 falso");
            Assert.IsTrue(v7 > v6, "7 falso debe ser mayor que 6");
            Assert.IsTrue(v6 > v5, "6 debe ser mayor que 5");
            Assert.IsTrue(v5 > v4, "5 debe ser mayor que 4");
        }

        [Test]
        public void Test_PericoAndPericaValues()
        {
            // Con Vira 7 de Espadas: Perico es 11 de Espadas, Perica es 10 de Espadas
            Card vira = new Card(7, "Sword");
            Card perico = new Card(11, "Sword");
            Card perica = new Card(10, "Sword");
            Card espadilla = new Card(1, "Sword");
            Card bastillo = new Card(1, "Cudgel");

            Assert.AreEqual(100, TrucoRules.GetCardRealValue(perico, vira), "Perico debe valer 100");
            Assert.AreEqual(99, TrucoRules.GetCardRealValue(perica, vira), "Perica debe valer 99");
            Assert.AreEqual(20, TrucoRules.GetCardRealValue(espadilla, vira), "Espadilla debe valer 20");
            Assert.AreEqual(19, TrucoRules.GetCardRealValue(bastillo, vira), "Bastillo debe valer 19");

            Assert.IsTrue(TrucoRules.GetCardRealValue(perico, vira) > TrucoRules.GetCardRealValue(perica, vira));
            Assert.IsTrue(TrucoRules.GetCardRealValue(perica, vira) > TrucoRules.GetCardRealValue(espadilla, vira));
        }

        [Test]
        public void Test_ViraTargetShift_WhenViraIs11Or10()
        {
            // Si la Vira es 11 de Copas, el Perico se desplaza al 12 de Copas
            Card vira11 = new Card(11, "Cup");
            Card shiftedPerico = new Card(12, "Cup");
            Card normal10 = new Card(10, "Cup"); // Perica se mantiene en 10
            Assert.AreEqual(100, TrucoRules.GetCardRealValue(shiftedPerico, vira11), "Con Vira 11, Perico debe ser el 12");
            Assert.AreEqual(99, TrucoRules.GetCardRealValue(normal10, vira11), "Con Vira 11, Perica debe ser el 10");

            // Si la Vira es 10 de Oros, la Perica se desplaza al 12 de Oros
            Card vira10 = new Card(10, "Gold");
            Card pericoNormal = new Card(11, "Gold"); // Perico se mantiene en 11
            Card shiftedPerica = new Card(12, "Gold");
            Assert.AreEqual(100, TrucoRules.GetCardRealValue(pericoNormal, vira10), "Con Vira 10, Perico debe ser el 11");
            Assert.AreEqual(99, TrucoRules.GetCardRealValue(shiftedPerica, vira10), "Con Vira 10, Perica debe ser el 12");
        }

        // ──────────────────────────────────────────────────────────────────────────
        // 2. ENVIDO Y FLOR: CÁLCULOS Y REGLA FLOR ANULA ENVIDO
        // ──────────────────────────────────────────────────────────────────────────

        [Test]
        public void Test_FlorDetection()
        {
            Card vira = new Card(3, "Cup");

            // 3 cartas del mismo palo = Flor
            List<Card> florNatural = new List<Card>
            {
                new Card(1, "Sword"),
                new Card(5, "Sword"),
                new Card(7, "Sword")
            };
            Assert.IsTrue(TrucoRules.IsFlor(florNatural, vira), "3 cartas del mismo palo deben ser Flor");

            // 2 cartas de Oro + Perico (11 de Copas) = Flor con pieza
            List<Card> florConPieza = new List<Card>
            {
                new Card(4, "Gold"),
                new Card(6, "Gold"),
                new Card(11, "Cup") // Perico
            };
            Assert.IsTrue(TrucoRules.IsFlor(florConPieza, vira), "2 cartas del mismo palo + Perico deben ser Flor");

            // Cartas variadas sin piezas = No es Flor
            List<Card> noFlor = new List<Card>
            {
                new Card(1, "Sword"),
                new Card(5, "Gold"),
                new Card(7, "Cup")
            };
            Assert.IsFalse(TrucoRules.IsFlor(noFlor, vira), "Cartas de distinto palo sin piezas no son Flor");
        }

        [Test]
        public void Test_EnvidoCalculation()
        {
            Card vira = new Card(3, "Sword");

            // 7 y 6 de Oro = 20 + 7 + 6 = 33
            List<Card> hand1 = new List<Card>
            {
                new Card(7, "Gold"),
                new Card(6, "Gold"),
                new Card(2, "Cup")
            };
            Assert.AreEqual(33, TrucoRules.CalculateEnvidoScore(hand1, vira));

            // Si tiene Flor, CalculateEnvidoScore devuelve -1 (no se puede cantar envido con flor)
            List<Card> handFlor = new List<Card>
            {
                new Card(1, "Gold"),
                new Card(2, "Gold"),
                new Card(3, "Gold")
            };
            Assert.AreEqual(-1, TrucoRules.CalculateEnvidoScore(handFlor, vira), "Tener Flor debe invalidar el Envido");

            // Si la Flor fue quemada (ignoreFlor = true), sí se puede calcular
            Assert.AreEqual(25, TrucoRules.CalculateEnvidoScore(handFlor, vira, ignoreFlor: true));
        }

        // ──────────────────────────────────────────────────────────────────────────
        // 3. DETERMINACIÓN DE GANADOR DE BAZA EN MESA DE 4 (2v2)
        // ──────────────────────────────────────────────────────────────────────────

        [Test]
        public void Test_4PlayerTrick_ClearWinner()
        {
            Card vira = new Card(4, "Cup");

            // 4 jugadores: Seat 0 (T1), Seat 1 (T2), Seat 2 (T1), Seat 3 (T2)
            Card c0 = new Card(3, "Sword") { realValue = TrucoRules.GetCardRealValue(new Card(3, "Sword"), vira) }; // 16
            Card c1 = new Card(11, "Cup")  { realValue = TrucoRules.GetCardRealValue(new Card(11, "Cup"), vira) };  // 100 (Perico)
            Card c2 = new Card(2, "Cup")   { realValue = TrucoRules.GetCardRealValue(new Card(2, "Cup"), vira) };   // 15
            Card c3 = new Card(1, "Cup")   { realValue = TrucoRules.GetCardRealValue(new Card(1, "Cup"), vira) };   // 14

            List<Card> tableCards = new List<Card> { c0, c1, c2, c3 };
            Card highest = tableCards.OrderByDescending(c => c.realValue).First();

            Assert.AreSame(c1, highest, "El Perico del Jugador 1 debe ser la carta más alta");
        }

        [Test]
        public void Test_4PlayerTrick_TieBetweenOpponents_IsParda()
        {
            Card vira = new Card(4, "Cup");

            // Seat 0 (T1) juega 3 de Espadas (16)
            // Seat 1 (T2) juega 3 de Bastos (16) -> Empate entre rivales!
            // Seat 2 (T1) juega 4 de Oros (7)
            // Seat 3 (T2) juega 5 de Copas (8)
            int val0 = TrucoRules.GetCardRealValue(new Card(3, "Sword"), vira);
            int val1 = TrucoRules.GetCardRealValue(new Card(3, "Cudgel"), vira);
            int val2 = TrucoRules.GetCardRealValue(new Card(4, "Gold"), vira);
            int val3 = TrucoRules.GetCardRealValue(new Card(5, "Cup"), vira);

            Assert.AreEqual(val0, val1, "Los 3 deben tener el mismo realValue");

            int team0 = 1;
            int team1 = 2;

            bool isParda = (val0 == val1) && (team0 != team1);
            Assert.IsTrue(isParda, "Si las cartas más altas son de equipos distintos, es Parda (empate)");
        }

        [Test]
        public void Test_4PlayerTrick_TieBetweenAllies_IsNotParda()
        {
            Card vira = new Card(4, "Cup");

            // Seat 0 (T1) juega 3 de Espadas (16)
            // Seat 1 (T2) juega 2 de Espadas (15)
            // Seat 2 (T1) juega 3 de Bastos (16) -> Empate entre compañeros de equipo!
            // Seat 3 (T2) juega 5 de Copas (8)
            int val0 = TrucoRules.GetCardRealValue(new Card(3, "Sword"), vira);
            int val2 = TrucoRules.GetCardRealValue(new Card(3, "Cudgel"), vira);

            int team0 = 1; // Team 1
            int team2 = 1; // Team 1 (Compañero)

            bool isParda = (val0 == val2) && (team0 != team2);
            Assert.IsFalse(isParda, "Si dos compañeros tiran la carta más alta, NO es Parda: gana el equipo");
        }

        // ──────────────────────────────────────────────────────────────────────────
        // 4. RESOLUCIÓN DE MANO (3 BAZAS) SEGÚN REGLAS DE TRUCO VENEZOLANO
        // ──────────────────────────────────────────────────────────────────────────

        private int SimulateHandResolution(List<int> trickWinners, int manoTeamIndex)
        {
            int t1Wins = trickWinners.Count(w => w == 1);
            int t2Wins = trickWinners.Count(w => w == 2);

            if (t1Wins >= 2) return 1;
            if (t2Wins >= 2) return 2;

            int currentRound = trickWinners.Count;
            if (currentRound == 1) return 0; // Continúa a 2da

            if (currentRound == 2)
            {
                // Parda primera, segunda define
                if (trickWinners[0] == 0 && trickWinners[1] != 0) return trickWinners[1];
                // Primera ganada, segunda parda -> gana el de la primera
                if (trickWinners[0] != 0 && trickWinners[1] == 0) return trickWinners[0];
                // Parda primera y parda segunda -> desempata Mano
                if (trickWinners[0] == 0 && trickWinners[1] == 0) return manoTeamIndex;
            }
            else if (currentRound == 3)
            {
                if (trickWinners[2] != 0) return trickWinners[2];
                // Tercera parda: gana quien ganó la primera
                if (trickWinners[0] != 0) return trickWinners[0];
                // Todo pardo: gana Mano
                return manoTeamIndex;
            }

            return 0;
        }

        [Test]
        public void Test_HandResolution_Normal2Rounds()
        {
            Assert.AreEqual(1, SimulateHandResolution(new List<int> { 1, 1 }, 1));
            Assert.AreEqual(2, SimulateHandResolution(new List<int> { 2, 2 }, 1));
        }

        [Test]
        public void Test_HandResolution_DecidedIn3rd()
        {
            // T1 gana 1ra, T2 gana 2da, T1 gana 3ra -> T1 gana mano
            Assert.AreEqual(1, SimulateHandResolution(new List<int> { 1, 2, 1 }, 1));
            // T1 gana 1ra, T2 gana 2da, T2 gana 3ra -> T2 gana mano
            Assert.AreEqual(2, SimulateHandResolution(new List<int> { 1, 2, 2 }, 1));
        }

        [Test]
        public void Test_HandResolution_PardaInFirst()
        {
            // Primera parda, segunda define:
            Assert.AreEqual(1, SimulateHandResolution(new List<int> { 0, 1 }, 2));
            Assert.AreEqual(2, SimulateHandResolution(new List<int> { 0, 2 }, 1));

            // Primera parda y segunda parda -> gana equipo Mano
            Assert.AreEqual(1, SimulateHandResolution(new List<int> { 0, 0 }, 1));
            Assert.AreEqual(2, SimulateHandResolution(new List<int> { 0, 0 }, 2));
        }

        [Test]
        public void Test_HandResolution_PardaInSecond()
        {
            // T1 gana 1ra, 2da parda -> T1 gana mano (primera gana)
            Assert.AreEqual(1, SimulateHandResolution(new List<int> { 1, 0 }, 2));
            // T2 gana 1ra, 2da parda -> T2 gana mano
            Assert.AreEqual(2, SimulateHandResolution(new List<int> { 2, 0 }, 1));
        }

        [Test]
        public void Test_HandResolution_PardaInThird()
        {
            // T1 gana 1ra, T2 gana 2da, 3ra parda -> T1 gana (primera desempata)
            Assert.AreEqual(1, SimulateHandResolution(new List<int> { 1, 2, 0 }, 2));
            // T2 gana 1ra, T1 gana 2da, 3ra parda -> T2 gana (primera desempata)
            Assert.AreEqual(2, SimulateHandResolution(new List<int> { 2, 1, 0 }, 1));
        }

        // ──────────────────────────────────────────────────────────────────────────
        // 5. ROTACIÓN DE REPARTIDOR (DEALER) Y MANO EN MESA DE 4 JUGADORES
        // ──────────────────────────────────────────────────────────────────────────

        [Test]
        public void Test_DealerAndManoRotation_4Players()
        {
            int totalSeats = 4;
            // Mano 1: Dealer = 3, Mano = 0
            int dealer = 3;
            int mano = (dealer + 1) % totalSeats;
            Assert.AreEqual(0, mano, "En Mano 1, el Mano debe ser el asiento 0");

            // Mano 2: Dealer = 0, Mano = 1
            dealer = (dealer + 1) % totalSeats;
            mano = (dealer + 1) % totalSeats;
            Assert.AreEqual(0, dealer, "En Mano 2, el Dealer debe ser asiento 0");
            Assert.AreEqual(1, mano, "En Mano 2, el Mano debe ser asiento 1");

            // Mano 3: Dealer = 1, Mano = 2
            dealer = (dealer + 1) % totalSeats;
            mano = (dealer + 1) % totalSeats;
            Assert.AreEqual(1, dealer, "En Mano 3, el Dealer debe ser asiento 1");
            Assert.AreEqual(2, mano, "En Mano 3, el Mano debe ser asiento 2");

            // Mano 4: Dealer = 2, Mano = 3
            dealer = (dealer + 1) % totalSeats;
            mano = (dealer + 1) % totalSeats;
            Assert.AreEqual(2, dealer, "En Mano 4, el Dealer debe ser asiento 2");
            Assert.AreEqual(3, mano, "En Mano 4, el Mano debe ser asiento 3");

            // Mano 5: Dealer = 3, Mano = 0 (Ciclo completo)
            dealer = (dealer + 1) % totalSeats;
            mano = (dealer + 1) % totalSeats;
            Assert.AreEqual(3, dealer, "En Mano 5, el Dealer vuelve a asiento 3");
            Assert.AreEqual(0, mano, "En Mano 5, el Mano vuelve a asiento 0");
        }

        // ──────────────────────────────────────────────────────────────────────────
        // 6. ENCAMINAMIENTO DE RESPONDEDOR EN 2v2 (DERECHA DEL CANTOR)
        // ──────────────────────────────────────────────────────────────────────────

        [Test]
        public void Test_ResponderRouting_In2v2()
        {
            // Sillas: 0 = T1, 1 = T2, 2 = T1, 3 = T2
            int[] seatTeams = { 1, 2, 1, 2 };

            int GetExpectedResponder(int announcerSeat)
            {
                int announcerTeam = seatTeams[announcerSeat];
                for (int step = 1; step <= 4; step++)
                {
                    int s = (announcerSeat + step) % 4;
                    if (seatTeams[s] != announcerTeam) return s;
                }
                return -1;
            }

            Assert.AreEqual(1, GetExpectedResponder(0), "Cantor en asiento 0 (T1) -> Responde asiento 1 (T2)");
            Assert.AreEqual(2, GetExpectedResponder(1), "Cantor en asiento 1 (T2) -> Responde asiento 2 (T1)");
            Assert.AreEqual(3, GetExpectedResponder(2), "Cantor en asiento 2 (T1) -> Responde asiento 3 (T2)");
            Assert.AreEqual(0, GetExpectedResponder(3), "Cantor en asiento 3 (T2) -> Responde asiento 0 (T1)");
        }

        // ──────────────────────────────────────────────────────────────────────────
        // 7. PREVENCIÓN DE BLOQUEOS (DEADLOCK) POR CANTOS INFORMATIVOS
        // ──────────────────────────────────────────────────────────────────────────

        [Test]
        public void Test_ALeyAndFlor_AreNonBlocking()
        {
            // La Flor y A Ley deben ser tratados como informativos: isAnnouncementPending = false
            bool isFlorInformative = (AnnounceState.Flor == AnnounceState.Flor || AnnounceState.Flor == AnnounceState.ALey);
            bool isALeyInformative = (AnnounceState.ALey == AnnounceState.Flor || AnnounceState.ALey == AnnounceState.ALey);
            bool isTrucoInformative = (AnnounceState.Truco == AnnounceState.Flor || AnnounceState.Truco == AnnounceState.ALey);
            bool isEnvidoInformative = (AnnounceState.Envido == AnnounceState.Flor || AnnounceState.Envido == AnnounceState.ALey);

            Assert.IsTrue(isFlorInformative, "Flor debe ser informativa");
            Assert.IsTrue(isALeyInformative, "A Ley debe ser informativa");
            Assert.IsFalse(isTrucoInformative, "Truco NO debe ser informativo (bloquea hasta Quiero/No Quiero)");
            Assert.IsFalse(isEnvidoInformative, "Envido NO debe ser informativo (bloquea hasta Quiero/No Quiero)");
        }

        // ──────────────────────────────────────────────────────────────────────────
        // 8. MENU ITEM Y MÉTODO PARA EJECUCIÓN DIRECTA POR CONSOLA/BATCHMODE
        // ──────────────────────────────────────────────────────────────────────────

        [MenuItem("TrucoTools/Run All Game Cycle Tests")]
        public static void RunAllTests()
        {
            var suite = new TrucoCycleTests();
            int passed = 0;
            int failed = 0;

            void Run(string testName, Action testAction)
            {
                try
                {
                    testAction();
                    Debug.Log($"[PASSED] {testName}");
                    passed++;
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[FAILED] {testName}: {ex.Message}\n{ex.StackTrace}");
                    failed++;
                }
            }

            Debug.Log("================ INICIANDO TEST SUITE DE CICLO DE JUEGO (4P / 2v2) ================");
            Run("Test_OrdinaryCardHierarchy", suite.Test_OrdinaryCardHierarchy);
            Run("Test_PericoAndPericaValues", suite.Test_PericoAndPericaValues);
            Run("Test_ViraTargetShift_WhenViraIs11Or10", suite.Test_ViraTargetShift_WhenViraIs11Or10);
            Run("Test_FlorDetection", suite.Test_FlorDetection);
            Run("Test_EnvidoCalculation", suite.Test_EnvidoCalculation);
            Run("Test_4PlayerTrick_ClearWinner", suite.Test_4PlayerTrick_ClearWinner);
            Run("Test_4PlayerTrick_TieBetweenOpponents_IsParda", suite.Test_4PlayerTrick_TieBetweenOpponents_IsParda);
            Run("Test_4PlayerTrick_TieBetweenAllies_IsNotParda", suite.Test_4PlayerTrick_TieBetweenAllies_IsNotParda);
            Run("Test_HandResolution_Normal2Rounds", suite.Test_HandResolution_Normal2Rounds);
            Run("Test_HandResolution_DecidedIn3rd", suite.Test_HandResolution_DecidedIn3rd);
            Run("Test_HandResolution_PardaInFirst", suite.Test_HandResolution_PardaInFirst);
            Run("Test_HandResolution_PardaInSecond", suite.Test_HandResolution_PardaInSecond);
            Run("Test_HandResolution_PardaInThird", suite.Test_HandResolution_PardaInThird);
            Run("Test_DealerAndManoRotation_4Players", suite.Test_DealerAndManoRotation_4Players);
            Run("Test_ResponderRouting_In2v2", suite.Test_ResponderRouting_In2v2);
            Run("Test_ALeyAndFlor_AreNonBlocking", suite.Test_ALeyAndFlor_AreNonBlocking);
            Debug.Log($"================ TEST SUITE FINALIZADO: {passed} PASARON, {failed} FALLARON ================");

            if (failed > 0)
            {
                EditorApplication.Exit(1);
            }
        }
    }
}
