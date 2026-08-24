import sys
import unittest
from datetime import date
from pathlib import Path


sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

from m702_strategy_comparison import (  # noqa: E402
    DotNetRandom,
    Draw,
    accumulator,
    add_portfolio_result,
    brier_loss,
    mix_probabilities,
    stable_seed,
    update_expert_weights,
)


class DotNetRandomTests(unittest.TestCase):
    def test_seeded_sequence_matches_system_random(self) -> None:
        random = DotNetRandom(202634)

        values = [random.next_double() for _ in range(5)]

        self.assertEqual(
            values,
            [
                0.8573586707270512,
                0.4054821037712889,
                0.5004851238338673,
                0.3529574425671983,
                0.5674247860756818,
            ],
        )


class CategoryScoringTests(unittest.TestCase):
    def test_scores_every_main_category_and_special_as_nested(self) -> None:
        draw_date = date(2026, 8, 24)
        first_ticket_reintegro = DotNetRandom(stable_seed(draw_date, 0, 97)).next_int(10)
        target = Draw(draw_date, (1, 2, 3, 4, 5, 6), 7, first_ticket_reintegro)
        portfolio = [
            (1, 2, 3, 4, 5, 6),
            (1, 2, 3, 4, 5, 7),
            (1, 2, 3, 4, 5, 8),
            (1, 2, 3, 4, 8, 9),
            (1, 2, 3, 8, 9, 10),
        ]
        result = accumulator("test", "test")

        add_portfolio_result(result, portfolio, target)

        self.assertEqual(result["specialSixPlusReintegro"], 1)
        self.assertEqual(result["firstSix"], 1)
        self.assertEqual(result["secondFivePlusComplementary"], 1)
        self.assertEqual(result["thirdFive"], 1)
        self.assertEqual(result["fourthFour"], 1)
        self.assertEqual(result["fifthThree"], 1)
        self.assertEqual(result["mainPrizeTickets"], 5)
        self.assertGreaterEqual(result["reintegro"], 1)


class AdaptiveEnsembleTests(unittest.TestCase):
    def test_brier_update_rewards_the_expert_closest_to_observed_draw(self) -> None:
        actual_numbers = (1, 2, 3, 4, 5, 6)
        uniform = [1.0 / 49.0] * 49
        specialist = [0.16 / 43.0] * 49
        for number in actual_numbers:
            specialist[number - 1] = 0.14

        updated, losses = update_expert_weights(
            [0.5, 0.5],
            [uniform, specialist],
            actual_numbers,
            learning_rate=20.0,
        )

        self.assertLess(losses[1], losses[0])
        self.assertGreater(updated[1], updated[0])
        self.assertAlmostEqual(sum(updated), 1.0)

    def test_regularized_mixture_is_normalized_and_shrunk_toward_uniform(self) -> None:
        concentrated = [0.0] * 49
        concentrated[0] = 1.0

        mixed = mix_probabilities(
            [[1.0 / 49.0] * 49, concentrated],
            [0.25, 0.75],
            uniform_shrinkage=0.20,
        )

        self.assertAlmostEqual(sum(mixed), 1.0)
        self.assertGreater(mixed[1], 0.0)
        self.assertLess(mixed[0], 0.75 + 0.25 / 49.0)
        self.assertGreater(
            brier_loss([[1.0 / 49.0] * 49][0], (1, 2, 3, 4, 5, 6)),
            0.0,
        )


if __name__ == "__main__":
    unittest.main()
