# Mapa do Indeed (br.indeed.com), outubro de 2026

Implementado em `Indeed.cs` (origem "Indeed" na tela Candidatar; ids `indeed-<jk>` no histórico).

Lido no Chrome com a conta logada, sem enviar candidatura. Seletores mudam: confira antes de automatizar.

## Busca: `https://br.indeed.com/jobs`
- Parâmetros: `q` (termos), `l` (local), `sc=0kf:attr(DSQF7);` (remoto), `start=0,10,20…` (paginação, ~15 cards por página).
- Card: `a[data-jk]` dentro de `.job_seen_beacon`. **`data-jk` é o id da vaga** (equivale ao jobId do LinkedIn).
- Empresa `[data-testid=company-name]`, local `[data-testid=text-location]`.
- Candidatura simplificada (o "Easy Apply" do Indeed): o card contém o texto **"Candidate-se facilmente"** (sem testid; detectar pelo texto).
- Próxima página: `[data-testid=pagination-page-next]`.
- Filtros (botões): `remote_filter_button`, `salaryType_filter_button`, `fromAge_filter_button`, `taxo1_filter_button` (linguagem).

## Painel da vaga (lado direito da busca)
- Título `[data-testid=vj-job-title]`, descrição `[data-testid=viewjob-job-content]`.
- Botão **Candidatar-se**: `[data-testid=viewjob-indeed-apply]` (link `target=_blank`): **abre o formulário numa aba nova** em `smartapply.indeed.com`.

## Formulário: `https://smartapply.indeed.com/beta/indeedapply/form/...`
Cada etapa tem uma URL própria; o progresso fica em `[role=progressbar]` (`aria-valuenow`).

| Etapa | Caminho | Progresso | O que tem |
|---|---|---|---|
| Contato | `contact-info-module` | 13% | `names-first-name`, `names-last-name`, `phone` (vêm preenchidos da conta; e-mail só leitura) |
| Currículo | `resume-selection-module/resume-selection` | 38% | rádio `resume-selection` já marcado com o currículo da conta; trocar em `ResumeOptionsMenu` |
| Perguntas da empresa | `questions-module/questions/1` (pode haver /2…) | 50% | ver abaixo |
| Autoidentificação (algumas vagas) | ? | 88% | `fieldset[data-testid=multi-select-question]` fora de `.ia-Questions-item`; consentimento "Aceitar" obrigatório (asterisco em `[data-testid$=-label-asterisk]`); botão "Verificar sua candidatura" em vez de "Continuar" |
| Revisão / envio | `review-module` | 100% | resumo; botão final `[data-testid=submit-application-button]` ("Enviar sua candidatura"); checkbox de e-mail com vagas (deixar desmarcado) |

Na vaga mapeada as perguntas couberam numa página só (`questions/1` → `review-module`). O botão final não tem cópias falsas;
para o modo Teste, parar em `review-module` sem clicar nele (equivale a descartar no LinkedIn). "Voltar" e "O que a empresa vê" também aparecem na revisão.

### Perguntas (`questions-module`)
- Cada pergunta: `div.ia-Questions-item#q_N`. O `name` do campo é `q_<hash>` (estável por pergunta).
- Texto livre: `input[type=text]` com `data-testid=input-q_<hash>-input`; pergunta no `<label>`.
- Número: também `type=text` (id `number-input-…`), ex.: "Pretensão salarial".
- Escolha única: `input[type=radio]` (id `single-select-question-…`), pergunta no `<legend>` do `fieldset`, opção no `<label>`.
- Múltipla escolha: `input[type=checkbox]` (id `multi-select-question-…`), mesmo esquema.
- Obrigatória: `*` no fim do label/legend, ou `required` no input.
- Várias perguntas são **testes técnicos com resposta certa** (cenários de ASP.NET/PostgreSQL, uso de IA, trabalho remoto): o Claude precisa escolher a melhor opção, não só responder pelo perfil.

## Armadilhas anti-robô
- **Botões "Continuar" falsos (honeypot)**: cada etapa tem 4 a 6 botões "Continuar"; só **um** é visível. Na etapa de currículo os falsos têm `data-testid=hp-continue-button-N`; no contato os testids vêm embaralhados. Regra: clicar só no "Continuar" **visível** (`offsetParent` não nulo e com largura), nunca por texto/testid sozinho.
- A página é protegida por **reCAPTCHA** (rodapé). Se aparecer desafio, parar e avisar o usuário; nunca tentar resolver.
- "Salvar e fechar" (`ExitLinkWithModalComponent-exitButton`) guarda rascunho.

## Diferenças para o LinkedIn que pesam no código
- Formulário em **aba nova** e em outro domínio (smartapply), não num modal da mesma página.
- Etapas por URL: dá para saber em que etapa está pelo caminho, sem ler o título.
- Contato e currículo já vêm prontos; o trabalho do Claude fica quase todo em `questions-module`.
