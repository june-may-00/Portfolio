<?php
    $mysql = new mysqli('localhost', 'junemay00', 'password', 'junemay00');

    $user_srl = intval($_POST['user_srl']);

    $query = $mysql->query("SELECT T1.* FROM `1917_pandemic_ranking` AS T1, (SELECT srl, max(score) as max_score from `1917_pandemic_ranking` GROUP BY user_srl) AS T2 WHERE T1.score = T2.max_score ORDER BY score DESC;");

    $result = [];
    while ($assoc = $query->fetch_assoc()) {
        $result[] = (object) $assoc;
    }

    $query = $mysql->query("SELECT * FROM `1917_pandemic_ranking` WHERE user_srl = {$user_srl} ORDER BY score DESC LIMIT 1;");
    $my_info = (object) $query->fetch_assoc();

    die(json_encode(['ranking' => $result, 'my_info' => $my_info]));
?>
