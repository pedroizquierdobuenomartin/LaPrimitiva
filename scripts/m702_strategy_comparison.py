from __future__ import annotations

import argparse
import csv
import json
import math
from dataclasses import dataclass
from datetime import UTC, date, datetime
from pathlib import Path


MBIG = 2_147_483_647
MSEED = 161_803_398


class DotNetRandom:
    """Compatibility implementation used by System.Random(int seed)."""

    def __init__(self, seed: int) -> None:
        subtraction = MBIG if seed == -2_147_483_648 else abs(seed)
        mj = MSEED - subtraction
        if mj < 0:
            mj += MBIG
        self.seed_array = [0] * 56
        self.seed_array[55] = mj
        mk = 1
        for i in range(1, 55):
            ii = 21 * i % 55
            self.seed_array[ii] = mk
            mk = mj - mk
            if mk < 0:
                mk += MBIG
            mj = self.seed_array[ii]
        for _ in range(4):
            for i in range(1, 56):
                self.seed_array[i] -= self.seed_array[1 + (i + 30) % 55]
                if self.seed_array[i] < 0:
                    self.seed_array[i] += MBIG
        self.inext = 0
        self.inextp = 21

    def internal_sample(self) -> int:
        self.inext += 1
        if self.inext >= 56:
            self.inext = 1
        self.inextp += 1
        if self.inextp >= 56:
            self.inextp = 1
        result = self.seed_array[self.inext] - self.seed_array[self.inextp]
        if result == MBIG:
            result -= 1
        if result < 0:
            result += MBIG
        self.seed_array[self.inext] = result
        return result

    def next_double(self) -> float:
        return self.internal_sample() * (1.0 / MBIG)

    def next_int(self, maximum: int) -> int:
        return int(self.next_double() * maximum)


@dataclass(frozen=True)
class Draw:
    draw_date: date
    numbers: tuple[int, ...]
    complementary: int
    reintegro: int


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser()
    parser.add_argument("--input", required=True, type=Path)
    parser.add_argument("--output", required=True, type=Path)
    parser.add_argument("--database", default="PrimitivaAuditV2")
    parser.add_argument("--minimum-training-draws", type=int, default=104)
    parser.add_argument("--portfolio-size", type=int, default=5)
    return parser.parse_args()


def load_draws(path: Path) -> list[Draw]:
    draws: list[Draw] = []
    with path.open("r", encoding="utf-8-sig", newline="") as source:
        for row in csv.reader(source, delimiter=";"):
            if not row or not row[0].strip():
                continue
            values = [value.strip() for value in row]
            draws.append(
                Draw(
                    draw_date=datetime.strptime(values[0], "%Y-%m-%d").date(),
                    numbers=tuple(int(value) for value in values[1:7]),
                    complementary=int(values[7]),
                    reintegro=int(values[8]),
                )
            )
    return draws


def week_seed(value: date) -> int:
    if 1 <= value.isoweekday() <= 3:
        from datetime import timedelta

        value += timedelta(days=3)
    iso_year, iso_week, _ = value.isocalendar()
    return iso_year * 100 + iso_week


def stable_seed(value: date, variation: int, salt: int) -> int:
    return (
        week_seed(value)
        + value.toordinal() * 7_919
        + variation * 104_729
        + salt * 1_000_003
    ) % MBIG


def uniform_numbers(seed: int) -> tuple[int, ...]:
    random = DotNetRandom(seed)
    numbers = list(range(1, 50))
    for index in range(len(numbers) - 1, 0, -1):
        swap_index = random.next_int(index + 1)
        numbers[index], numbers[swap_index] = numbers[swap_index], numbers[index]
    return tuple(sorted(numbers[:6]))


def weighted_numbers(probabilities: list[float], seed: int) -> tuple[int, ...]:
    random = DotNetRandom(seed)
    available = list(range(1, 50))
    weights = probabilities.copy()
    picked: list[int] = []
    for _ in range(6):
        value = random.next_double() * sum(weights)
        cumulative = 0.0
        selected_index = len(weights) - 1
        for index, weight in enumerate(weights):
            cumulative += weight
            if value < cumulative:
                selected_index = index
                break
        picked.append(available.pop(selected_index))
        weights.pop(selected_index)
    return tuple(sorted(picked))


def ensemble_numbers(
    probabilities90: list[float],
    probabilities365: list[float],
    probabilities1825: list[float],
    pair_counts: list[list[float]],
    counts365: list[float],
    seed: int,
) -> tuple[int, ...]:
    random = DotNetRandom(seed)
    available = list(range(1, 50))
    picked: list[int] = []
    while len(picked) < 6:
        scores: list[float] = []
        for number in available:
            index = number - 1
            log_score = (
                0.25 * math.log(max(probabilities90[index], 1e-12))
                + 0.50 * math.log(max(probabilities365[index], 1e-12))
                + 0.25 * math.log(max(probabilities1825[index], 1e-12))
            )
            if picked:
                pair_log = 0.0
                for selected in picked:
                    selected_index = selected - 1
                    conditional = (pair_counts[selected_index][index] + 1.0) / (
                        5.0 * counts365[selected_index] + 48.0
                    )
                    lift = conditional / max(probabilities365[index], 1e-12)
                    pair_log += math.log(min(5.0, max(0.2, lift)))
                log_score += 0.15 * pair_log / len(picked)
            scores.append(math.exp(log_score))

        value = random.next_double() * sum(scores)
        cumulative = 0.0
        selected_index = len(scores) - 1
        for index, score in enumerate(scores):
            cumulative += score
            if value < cumulative:
                selected_index = index
                break
        picked.append(available.pop(selected_index))
    return tuple(sorted(picked))


def diverse_portfolio(
    probabilities: list[float], value: date, size: int, salt: int = 41
) -> list[tuple[int, ...]]:
    candidate_count = max(60, size * 15)
    candidates: list[tuple[tuple[int, ...], float]] = []
    seen: set[tuple[int, ...]] = set()
    for candidate_index in range(candidate_count):
        numbers = weighted_numbers(probabilities, stable_seed(value, candidate_index, salt))
        if numbers in seen:
            continue
        seen.add(numbers)
        score = sum(math.log(max(probabilities[number - 1], 1e-12)) for number in numbers)
        candidates.append((numbers, score))

    selected = [candidates[0]]
    while len(selected) < size:
        remaining = [candidate for candidate in candidates if candidate not in selected]
        best = min(
            remaining,
            key=lambda candidate: (
                sum(len(set(candidate[0]) & set(existing[0])) for existing in selected),
                max(len(set(candidate[0]) & set(existing[0])) for existing in selected),
                -candidate[1],
                candidate[0],
            ),
        )
        selected.append(best)
    return [candidate[0] for candidate in selected]


def accumulator(name: str, description: str) -> dict:
    return {
        "name": name,
        "description": description,
        "tickets": 0,
        "simulatedCostAtOneEuro": 0,
        "totalMainMatches": 0,
        "maximumMainMatches": 0,
        "specialSixPlusReintegro": 0,
        "firstSix": 0,
        "secondFivePlusComplementary": 0,
        "thirdFive": 0,
        "fourthFour": 0,
        "fifthThree": 0,
        "mainPrizeTickets": 0,
        "reintegro": 0,
        "anyPrizeTickets": 0,
        "drawsWithAnyPrize": 0,
        "matchDistribution": {str(value): 0 for value in range(7)},
        "mainMatchesByDraw": [],
        "mainPrizeTicketsByDraw": [],
    }


def add_portfolio_result(result: dict, portfolio: list[tuple[int, ...]], target: Draw) -> None:
    target_numbers = set(target.numbers)
    draw_has_prize = False
    draw_matches = 0
    draw_main_prizes = 0
    for ticket_index, ticket in enumerate(portfolio):
        matches = len(set(ticket) & target_numbers)
        draw_matches += matches
        reintegro = DotNetRandom(stable_seed(target.draw_date, ticket_index, 97)).next_int(10)
        reintegro_hit = reintegro == target.reintegro
        result["tickets"] += 1
        result["simulatedCostAtOneEuro"] += 1
        result["totalMainMatches"] += matches
        result["maximumMainMatches"] = max(result["maximumMainMatches"], matches)
        result["matchDistribution"][str(matches)] += 1
        if matches == 6:
            result["firstSix"] += 1
            if reintegro_hit:
                result["specialSixPlusReintegro"] += 1
        elif matches == 5 and target.complementary in ticket:
            result["secondFivePlusComplementary"] += 1
        elif matches == 5:
            result["thirdFive"] += 1
        elif matches == 4:
            result["fourthFour"] += 1
        elif matches == 3:
            result["fifthThree"] += 1
        if matches >= 3:
            result["mainPrizeTickets"] += 1
            draw_main_prizes += 1
        if reintegro_hit:
            result["reintegro"] += 1
        if matches >= 3 or reintegro_hit:
            result["anyPrizeTickets"] += 1
            draw_has_prize = True
    result["mainMatchesByDraw"].append(draw_matches)
    result["mainPrizeTicketsByDraw"].append(draw_main_prizes)
    if draw_has_prize:
        result["drawsWithAnyPrize"] += 1


def probabilities(counts: list[float]) -> list[float]:
    total = sum(counts)
    return [(count + 1.0) / (total + 49.0) for count in counts]


def brier_loss(probability_vector: list[float], actual_numbers: tuple[int, ...]) -> float:
    """Multi-label Brier loss for the six inclusions in a 6-of-49 draw."""
    actual = set(actual_numbers)
    return sum(
        (min(1.0, 6.0 * probability) - (1.0 if index + 1 in actual else 0.0)) ** 2
        for index, probability in enumerate(probability_vector)
    ) / 49.0


def update_expert_weights(
    weights: list[float],
    expert_probabilities: list[list[float]],
    actual_numbers: tuple[int, ...],
    learning_rate: float,
) -> tuple[list[float], list[float]]:
    """Exponentially reweight experts after observing the current draw."""
    losses = [brier_loss(expert, actual_numbers) for expert in expert_probabilities]
    unnormalized = [
        weight * math.exp(-learning_rate * loss)
        for weight, loss in zip(weights, losses, strict=True)
    ]
    total = sum(unnormalized)
    return [value / total for value in unnormalized], losses


def mix_probabilities(
    expert_probabilities: list[list[float]],
    expert_weights: list[float],
    uniform_shrinkage: float,
) -> list[float]:
    """Combine online experts and shrink the result toward the fair-draw baseline."""
    mixed = [
        sum(
            weight * expert[number_index]
            for weight, expert in zip(expert_weights, expert_probabilities, strict=True)
        )
        for number_index in range(49)
    ]
    regularized = [
        (1.0 - uniform_shrinkage) * value + uniform_shrinkage / 49.0
        for value in mixed
    ]
    total = sum(regularized)
    return [value / total for value in regularized]


def paired_z_score(candidate: list[int], baseline: list[int]) -> float:
    differences = [left - right for left, right in zip(candidate, baseline, strict=True)]
    if len(differences) < 2:
        return 0.0
    mean = sum(differences) / len(differences)
    variance = sum((value - mean) ** 2 for value in differences) / (len(differences) - 1)
    if variance == 0:
        return 0.0
    return mean / math.sqrt(variance / len(differences))


def main() -> None:
    args = parse_args()
    if args.minimum_training_draws < 1:
        raise ValueError("minimum-training-draws must be positive")
    if args.portfolio_size < 2:
        raise ValueError("portfolio-size must be at least two")
    draws = load_draws(args.input)
    if len(draws) <= args.minimum_training_draws:
        raise ValueError("not enough historical draws")

    counts90 = [0.0] * 49
    counts365 = [0.0] * 49
    counts1825 = [0.0] * 49
    pair_counts = [[0.0] * 49 for _ in range(49)]
    first_target_date = draws[args.minimum_training_draws].draw_date
    for draw in draws[: args.minimum_training_draws]:
        age_days = (first_target_date - draw.draw_date).days
        weights = (
            0.5 ** (age_days / 90.0),
            0.5 ** (age_days / 365.0),
            0.5 ** (age_days / 1825.0),
        )
        for number in draw.numbers:
            counts90[number - 1] += weights[0]
            counts365[number - 1] += weights[1]
            counts1825[number - 1] += weights[2]
        for left in range(6):
            for right in range(left + 1, 6):
                left_index = draw.numbers[left] - 1
                right_index = draw.numbers[right] - 1
                pair_counts[left_index][right_index] += weights[1]
                pair_counts[right_index][left_index] += weights[1]

    strategies = {
        "uniform": accumulator("Uniforme", "Selección uniforme sin reemplazo."),
        "currentWeighted": accumulator(
            "Ponderado actual",
            "Frecuencias marginales con vida media de 365 días y semilla específica por sorteo para una comparación justa.",
        ),
        "coverageDiverse": accumulator(
            "Cobertura diversificada",
            "Candidatas ponderadas seleccionadas para minimizar solapamiento entre cinco apuestas.",
        ),
        "temporalPairEnsemble": accumulator(
            "Ensemble temporal y pares",
            "Ventanas de 90, 365 y 1.825 días con afinidad regularizada de pares.",
        ),
        "adaptiveRegularizedEnsemble": accumulator(
            "Ensemble adaptativo regularizado",
            "Mezcla online de uniforme y ventanas de 90, 365 y 1.825 días, actualizada por pérdida Brier y diversificada.",
        ),
    }
    expert_names = ["uniform", "halfLife90", "halfLife365", "halfLife1825"]
    expert_weights = [0.25] * len(expert_names)
    expert_loss_sums = [0.0] * len(expert_names)
    learning_rate = 20.0
    uniform_shrinkage = 0.20

    for index in range(args.minimum_training_draws, len(draws)):
        target = draws[index]
        if index > args.minimum_training_draws:
            previous = draws[index - 1]
            age_days = (target.draw_date - previous.draw_date).days
            decays = (
                0.5 ** (age_days / 90.0),
                0.5 ** (age_days / 365.0),
                0.5 ** (age_days / 1825.0),
            )
            for number_index in range(49):
                counts90[number_index] *= decays[0]
                counts365[number_index] *= decays[1]
                counts1825[number_index] *= decays[2]
                row = pair_counts[number_index]
                for pair_index in range(49):
                    row[pair_index] *= decays[1]
            for number in previous.numbers:
                counts90[number - 1] += decays[0]
                counts365[number - 1] += decays[1]
                counts1825[number - 1] += decays[2]
            for left in range(6):
                for right in range(left + 1, 6):
                    left_index = previous.numbers[left] - 1
                    right_index = previous.numbers[right] - 1
                    pair_counts[left_index][right_index] += decays[1]
                    pair_counts[right_index][left_index] += decays[1]

        probabilities90 = probabilities(counts90)
        probabilities365 = probabilities(counts365)
        probabilities1825 = probabilities(counts1825)
        expert_probabilities = [
            [1.0 / 49.0] * 49,
            probabilities90,
            probabilities365,
            probabilities1825,
        ]
        adaptive_probabilities = mix_probabilities(
            expert_probabilities, expert_weights, uniform_shrinkage
        )
        uniform_portfolio = [
            uniform_numbers(stable_seed(target.draw_date, ticket_index, 11))
            for ticket_index in range(args.portfolio_size)
        ]
        current_portfolio = [
            weighted_numbers(probabilities365, stable_seed(target.draw_date, ticket_index, 21))
            for ticket_index in range(args.portfolio_size)
        ]
        coverage_portfolio = diverse_portfolio(
            probabilities365, target.draw_date, args.portfolio_size
        )
        ensemble_portfolio = [
            ensemble_numbers(
                probabilities90,
                probabilities365,
                probabilities1825,
                pair_counts,
                counts365,
                stable_seed(target.draw_date, ticket_index, 31),
            )
            for ticket_index in range(args.portfolio_size)
        ]
        adaptive_portfolio = diverse_portfolio(
            adaptive_probabilities, target.draw_date, args.portfolio_size, salt=51
        )
        add_portfolio_result(strategies["uniform"], uniform_portfolio, target)
        add_portfolio_result(strategies["currentWeighted"], current_portfolio, target)
        add_portfolio_result(strategies["coverageDiverse"], coverage_portfolio, target)
        add_portfolio_result(strategies["temporalPairEnsemble"], ensemble_portfolio, target)
        add_portfolio_result(
            strategies["adaptiveRegularizedEnsemble"], adaptive_portfolio, target
        )

        expert_weights, current_losses = update_expert_weights(
            expert_weights,
            expert_probabilities,
            target.numbers,
            learning_rate,
        )
        expert_loss_sums = [
            total + current
            for total, current in zip(expert_loss_sums, current_losses, strict=True)
        ]

    evaluated_draws = len(draws) - args.minimum_training_draws
    baseline_by_draw = strategies["uniform"]["mainMatchesByDraw"]
    baseline_main_prizes_by_draw = strategies["uniform"]["mainPrizeTicketsByDraw"]
    for strategy in strategies.values():
        strategy["averageMainMatchesPerTicket"] = round(
            strategy["totalMainMatches"] / strategy["tickets"], 6
        )
        strategy["mainPrizeRate"] = round(strategy["mainPrizeTickets"] / strategy["tickets"], 8)
        strategy["anyPrizeRate"] = round(strategy["anyPrizeTickets"] / strategy["tickets"], 8)
        strategy["pairedAverageMatchesVsUniformZ"] = round(
            paired_z_score(strategy["mainMatchesByDraw"], baseline_by_draw), 6
        )
        strategy["pairedMainPrizeTicketsVsUniformZ"] = round(
            paired_z_score(strategy["mainPrizeTicketsByDraw"], baseline_main_prizes_by_draw), 6
        )
        del strategy["mainMatchesByDraw"]
        del strategy["mainPrizeTicketsByDraw"]

    official_odds = {
        "specialSixPlusReintegro": 139_838_160,
        "firstSix": 13_983_816,
        "secondFivePlusComplementary": 2_330_636,
        "thirdFive": 55_491,
        "fourthFour": 1_032,
        "fifthThree": 57,
        "reintegro": 10,
    }
    ticket_count = evaluated_draws * args.portfolio_size
    theoretical_main_prize_probability = sum(
        1.0 / official_odds[category]
        for category in (
            "firstSix",
            "secondFivePlusComplementary",
            "thirdFive",
            "fourthFour",
            "fifthThree",
        )
    )
    for strategy in strategies.values():
        variance = ticket_count * theoretical_main_prize_probability * (
            1.0 - theoretical_main_prize_probability
        )
        strategy["mainPrizeTicketsVsOfficialOddsZ"] = round(
            (strategy["mainPrizeTickets"] - ticket_count * theoretical_main_prize_probability)
            / math.sqrt(variance),
            6,
        )
    evidence = {
        "milestone": "M-702",
        "generatedAt": datetime.now(UTC).isoformat(),
        "database": args.database,
        "methodology": {
            "mode": "walk-forward",
            "historicalDraws": len(draws),
            "minimumTrainingDraws": args.minimum_training_draws,
            "evaluatedDraws": evaluated_draws,
            "firstEvaluatedDate": draws[args.minimum_training_draws].draw_date.isoformat(),
            "lastEvaluatedDate": draws[-1].draw_date.isoformat(),
            "portfolioSizePerDraw": args.portfolio_size,
            "ticketsPerStrategy": ticket_count,
            "equalSimulatedCostPerStrategyAtOneEuro": ticket_count,
            "categorySemantics": (
                "FirstSix includes every 6-hit ticket; SpecialSixPlusReintegro is its nested subset. "
                "Reintegro is independently counted and identical deterministic digits are used across strategies."
            ),
            "adaptiveRegularizedEnsemble": {
                "experts": expert_names,
                "learningRule": "exponential weights updated after each evaluated draw using multi-label Brier loss",
                "learningRate": learning_rate,
                "uniformShrinkage": uniform_shrinkage,
                "finalExpertWeights": {
                    name: round(weight, 8)
                    for name, weight in zip(expert_names, expert_weights, strict=True)
                },
                "meanExpertBrierLoss": {
                    name: round(total / evaluated_draws, 8)
                    for name, total in zip(expert_names, expert_loss_sums, strict=True)
                },
                "leakageGuard": "Weights for a target draw are computed before that target is observed and updated only afterwards.",
            },
        },
        "officialApproximateOddsOneIn": official_odds,
        "theoreticalExpectedCategoryCountsPerStrategy": {
            category: round(ticket_count / odds, 6) for category, odds in official_odds.items()
        },
        "theoreticalMainPrizeProbabilityPerTicket": round(theoretical_main_prize_probability, 10),
        "strategies": strategies,
        "conclusionGuardrail": (
            "A higher historical category count is not evidence that a strategy can predict a fair future jackpot. "
            "Categories with expected counts far below one cannot be compared meaningfully with this history."
        ),
    }
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(evidence, indent=2, ensure_ascii=False), encoding="utf-8")
    print(json.dumps(evidence, indent=2, ensure_ascii=False))


if __name__ == "__main__":
    main()
