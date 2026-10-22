# Mapa do Coodesh (coodesh.com), outubro de 2026

Lido no Chrome com a conta logada, numa candidatura real (Tech Lead RPA | BITI9). Seletores mudam: confira antes de automatizar.
App React + Chakra UI; a página de vaga hidrata primeiro deslogada ("Login / Criar conta") e só depois mostra o botão.

## Lista: `https://coodesh.com/jobs`
- Busca: `?search=<termos>`. Sem busca, mostra as vagas "baseadas nas suas preferências".
- Total: `[data-testid=table-total]`. Próxima página: `button[aria-label="Próxima página"]` (`disabled` na última).
- Card: linha de tabela com `a[aria-label="Ver vaga"][href^="/pt/jobs/"]`.
- **Id da vaga: o número no fim do slug**, ex.: `/pt/jobs/desenvolvedor-rpa-191038516` → `191038516`.

## Vaga: `https://coodesh.com/pt/jobs/<slug>-<id>`
- Lateral: categoria, jornada, nível, **tipo de contratação (PJ/CLT)**, **faixa salarial**, **localidade (Remota / Híbrida em <cidade>)**.
- Corpo: descrição, "Habilidades" (tags), "Requisitos".
- Botão **"Tenho Interesse"** (`button[type=submit]`, aparece duas vezes; clicar no primeiro visível).
  Em algumas vagas "Tenho Interesse" é um `<a target=_blank>` para formulário de fora (forms.monday.com): pular.
  Cria a candidatura na hora e redireciona para `/pt/dashboard/applicants/<applicantId>?activeContentType=preferences&mode=edit`.

## Candidatura: `/pt/dashboard/applicants/<applicantId>`
Etapas: **Pré Candidatura** (Preferências + ScoreCard) → Triagem (recrutador) → Resultado Final.
Só depois das duas partes da pré-candidatura o perfil vai para a triagem. "Desistir" cancela.

### Preferências (modal `section[role=dialog]` "Dados da candidatura")
Vem preenchido do perfil Coodesh; só a pretensão costuma faltar.

| Campo | Seletor | Observação |
|---|---|---|
| Preferência para contato* | hidden `contact_preference` + `button[role=combobox]` | `email` |
| Whatsapp* | `input[type=tel]` | do perfil |
| Localização* | hidden `location` (`BR-SP-São Paulo`) + combobox | |
| Disponibilidade* | hidden `home_office` (`partial` = Híbrida) + combobox | |
| Mudar de cidade | checkbox `other_cities` | |
| Moeda | hidden `salary_range_jp.currency` (`BRL`) | |
| **Pretensão PJ*** | `input[name="salary_range_jp.min"]` (Chakra number input) | vem `0`; usar "Pretensão PJ" do perfil (CLT na vaga CLT, a conferir) |
| LinkedIn* / Github | `input[name=linkedin]`, `input[name=github]` | do perfil |
| Currículo | `input[type=file]` | já anexado no perfil |

Botão "Salvar" (`type=submit`) → toast "Salvo com sucesso!".

### ScoreCard ("Mapa de Habilidades"): abre em **aba nova**
- `https://coodesh.com/scorecard/<uuid>`: checkbox "Declaro que as informações são verídicas" + "Responder agora".
  A página tem reCAPTCHA (`g-recaptcha-response`).
- `/instructions`: **cronômetro regressivo (~19 min)**, legenda dos níveis, "Próximo".
- `/skills`: uma linha por habilidade da vaga (as tags dela; 8 nesta).
  - Nome em `div#skill-row-<skillId>`; botão `button#skill-row-<skillId>-level` ("Selecione o nível", `aria-haspopup=dialog`).
  - O botão abre `[role=dialog]` "Competência N de M" com `[role=radiogroup]` de 4 `input[type=radio]`:
    **Nenhum / Básico / Praticante / Avançado**. Botões "Próxima competência" e "Concluir".
  - Autosalva ("Salvo"). Fim: botão **"Finalizar"** na página → `/thank-you` ("Obrigado pelo tempo dedicado…").
  - Visto na candidatura mapeada: a etapa já mostrou "Você entrou na triagem" com o ScoreCard ainda em branco
    (depois de a aba fechar); a URL `/skills` continuou editável e foi finalizada depois. Não contar com isso: sempre finalizar.
- É **autoavaliação**: mapear de "Anos de experiência por competência" do perfil
  (0/"não domino" → Nenhum; só estudo → Básico; uso no trabalho → Praticante; muitos anos/lidera → Avançado).

## Diferenças para o Indeed/LinkedIn que pesam no código
- Formulário fixo, sem passo a passo e sem perguntas abertas: as respostas são a pretensão e o nível por habilidade.
- "Tenho Interesse" já cria a candidatura: o modo Teste tem que parar **antes** dele.
- O ScoreCard tem tempo e abre em outra aba.
