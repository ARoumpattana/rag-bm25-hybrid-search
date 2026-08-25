namespace KeyMatch.Prompts
{
    public static class ConfirmMatchPrompt
    {
        public static string ConfirmPrompt(string input, List<string> candidates)
        {
            return $"""
                [Instructions]
                You are an expert in international cargo and logistics classification.
                Given a cargo description and a list of candidates, select the single best matching candidate.
                The keyword label will always appear in the cargo description, either fully or partially.
                Prioritize the candidate whose words appear directly in the cargo description over semantic similarity.
                Translate the selected label to natural Thai that a logistics professional would understand (not transliteration).
                If no candidate matches or the input is gibberish/test text, reply with "ไม่พบ" only.
                Reply format: Thai translation (ENGLISH LABEL) — example: มะพร้าวสด (FRESH COCONUT)
                ONE answer only. No explanation. No extra text.

                [Examples]
                Cargo: XYZ123::ABC:SGSIN:INDUSTRIAL COOLING PAD UNIT:260101::
                Candidates: COOLING PAD, HEATER, STEEL
                Answer: แผ่นระบายความร้อน (COOLING PAD)

                Cargo: ABC456::DEF:USLAX:CHEMICAL ACID COMPOUND 99%:260202::
                Candidates: ACID, FOOD, RUBBER
                Answer: กรด (ACID)

                Cargo: RANDOM XYZ UNKNOWN TEST ITEM
                Candidates: TIRE, PAINTS
                Answer: ไม่พบ

                [Current Task]
                Cargo: {input}
                Candidates: {string.Join(", ", candidates)}
                Answer:
                """;
        }
    }
}
