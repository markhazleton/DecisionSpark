// START OF JAVASCRIPT TO INSERT (insert after line 513, before </script>)

        // Utility function to escape HTML
        function escapeHtml(text) {
            const div = document.createElement('div');
      div.textContent = text;
       return div.innerHTML;
        }

 // Start a new conversation
        async function startConversation() {
        try {
     showLoading('Starting conversation...');
                document.getElementById('startBtn').disabled = true;

      const requestUrl = `${BASE_URL}/start`;
       const requestHeaders = {
           'Content-Type': 'application/json',
    'X-API-KEY': API_KEY
                };
                const requestBody = {};

       addDebugEntry('success', '?? Starting Conversation', {
          url: requestUrl,
               method: 'POST',
 headers: requestHeaders,
    body: requestBody
     });

  const response = await fetch(requestUrl, {
         method: 'POST',
  headers: requestHeaders,
      body: JSON.stringify(requestBody)
       });

     const data = await response.json();

        addDebugEntry('success', `? Start Response (${response.status})`, {
                status: response.status,
                    statusText: response.statusText,
    headers: Object.fromEntries(response.headers.entries()),
   body: data
         });

      if (!response.ok) {
  throw new Error(`HTTP error! status: ${response.status}`);
         }

                handleStartResponse(data);
          } catch (error) {
                addDebugEntry('error', '? Start Error', {
          error: error.message,
 stack: error.stack
            });
   console.error('Error starting conversation:', error);
 addMessage('error', 'Failed to start conversation: ' + error.message);
    } finally {
                document.getElementById('startBtn').disabled = false;
   }
        }

     // Handle start response
        function handleStartResponse(data) {
            clearConversation();
       
            // Extract session ID from nextUrl
            if (data.nextUrl) {
const matches = data.nextUrl.match(/conversation\/([^\/]+)\//);
                if (matches) {
    currentSessionId = matches[1];
     currentNextUrl = data.nextUrl;
             updateSessionDisplay(currentSessionId, 'active');
            }
          }

   // Display welcome message
      if (data.texts && data.texts.length > 0) {
  addMessage('assistant', data.texts.join(' '));
            }

      // Display question
            if (data.question) {
        addMessage('assistant', data.question.text);
     showInputArea(data.question);
       }

    // Check if already complete
            if (data.isComplete) {
    handleCompletion(data);
            }
    }

        // Submit answer
   document.getElementById('answerForm').addEventListener('submit', async (e) => {
  e.preventDefault();
        
const userInput = document.getElementById('userInput').value.trim();
    if (!userInput || !currentNextUrl) return;

            try {
          document.getElementById('sendBtn').disabled = true;
      document.getElementById('sendBtn').innerHTML = '<span class="spinner-border spinner-border-sm me-2"></span>Sending...';

      // Show user's answer
          addMessage('user', userInput);
                document.getElementById('userInput').value = '';

  // Construct full URL
    const fullUrl = currentNextUrl.startsWith('http') 
      ? currentNextUrl 
     : `${BASE_URL}${currentNextUrl}`;

         const requestHeaders = {
             'Content-Type': 'application/json',
    'X-API-KEY': API_KEY
    };
   const requestBody = { user_input: userInput };

          addDebugEntry('success', '?? Sending Answer', {
       url: fullUrl,
           method: 'POST',
   headers: requestHeaders,
    body: requestBody,
      note: 'Property name: user_input (snake_case)'
   });

                const response = await fetch(fullUrl, {
        method: 'POST',
    headers: requestHeaders,
        body: JSON.stringify(requestBody)
   });

          const data = await response.json();

      const statusType = response.ok ? 'success' : 'error';
       const statusIcon = response.ok ? '?' : '?';
     
     addDebugEntry(statusType, `${statusIcon} Answer Response (${response.status})`, {
     status: response.status,
    statusText: response.statusText,
      headers: Object.fromEntries(response.headers.entries()),
     body: data
             });

     if (!response.ok) {
    handleErrorResponse(data);
            return;
     }

                handleNextResponse(data);
   } catch (error) {
        addDebugEntry('error', '? Answer Error', {
              error: error.message,
     stack: error.stack
           });
                console.error('Error submitting answer:', error);
      addMessage('error', 'Failed to submit answer: ' + error.message);
       } finally {
   document.getElementById('sendBtn').disabled = false;
       document.getElementById('sendBtn').innerHTML = '<i class="bi bi-send-fill"></i> Send Answer';
         }
        });

        // Handle next response
        function handleNextResponse(data) {
            // Update next URL
            if (data.nextUrl) {
       currentNextUrl = data.nextUrl;
   }

     // Display any text messages
            if (data.texts && data.texts.length > 0) {
addMessage('assistant', data.texts.join(' '));
  }

        // Display question if not complete
   if (!data.isComplete && data.question) {
       addMessage('assistant', data.question.text);
             showInputArea(data.question);
      }

        // Handle completion
         if (data.isComplete) {
     handleCompletion(data);
  }
     }

 // Handle error response
   function handleErrorResponse(data) {
            if (data.error) {
   const errorMsg = typeof data.error === 'string' ? data.error : data.error.message;
  addMessage('error', errorMsg);
  }

         // Show rephrased question if available
            if (data.question) {
    addMessage('assistant', data.question.text);
           showInputArea(data.question);
            }
  }

   // Handle conversation completion
  function handleCompletion(data) {
  updateSessionDisplay(currentSessionId, 'complete');
            document.getElementById('inputArea').style.display = 'none';

      if (data.displayCards && data.displayCards.length > 0) {
          const card = data.displayCards[0];
       addRecommendation(card);
            }

    if (data.finalResult) {
          addMessage('assistant', `? Recommendation complete: ${data.finalResult.outcomeId}`);
        }
     }

     // Add message to conversation
      function addMessage(type, content) {
            const conversationArea = document.getElementById('conversationArea');
        
      // Remove empty state
      if (conversationArea.querySelector('.text-muted')) {
     conversationArea.innerHTML = '';
     }

       const messageDiv = document.createElement('div');
    messageDiv.className = `message ${type}`;
            
      let icon = '';
 let label = '';
 
         switch(type) {
                case 'assistant':
    icon = '<i class="bi bi-robot"></i>';
         label = 'Assistant';
         break;
         case 'user':
   icon = '<i class="bi bi-person-fill"></i>';
        label = 'You';
      break;
case 'error':
      icon = '<i class="bi bi-exclamation-triangle-fill"></i>';
            label = 'Error';
      break;
            case 'recommendation':
       icon = '<i class="bi bi-star-fill"></i>';
      label = 'Recommendation';
          break;
   }

          messageDiv.innerHTML = `
    <div class="message-label">${icon} ${label}</div>
       <div class="message-content">${escapeHtml(content)}</div>
     `;

  conversationArea.appendChild(messageDiv);
            conversationArea.scrollTop = conversationArea.scrollHeight;
        }

        // Add recommendation card
        function addRecommendation(card) {
          const conversationArea = document.getElementById('conversationArea');
     
       const recDiv = document.createElement('div');
      recDiv.className = 'message recommendation';
            
 let detailsHtml = '';
            if (card.bodyText && card.bodyText.length > 0) {
         detailsHtml = '<ul class="recommendation-details">';
              card.bodyText.forEach(item => {
           detailsHtml += `<li>${escapeHtml(item)}</li>`;
  });
         detailsHtml += '</ul>';
      }

 recDiv.innerHTML = `
          <div class="recommendation-title">${escapeHtml(card.title)}</div>
        <p class="lead">${escapeHtml(card.subtitle)}</p>
           <p>${escapeHtml(card.careTypeMessage)}</p>
  ${detailsHtml}
       `;

         conversationArea.appendChild(recDiv);
    conversationArea.scrollTop = conversationArea.scrollHeight;
    }

        // Show input area with quick actions
        function showInputArea(question) {
   const inputArea = document.getElementById('inputArea');
  inputArea.style.display = 'block';
            
   // Set hint based on question type
    const hintDiv = document.getElementById('inputHint');
          if (question.allowFreeText) {
           hintDiv.textContent = 'Answer naturally. Examples: "5 people", "ages: 4, 9, 35, 40"';
   } else {
hintDiv.textContent = 'Select or type your answer';
     }

     // Add quick action examples
            const quickActionsDiv = document.getElementById('quickActions');
  quickActionsDiv.innerHTML = '';

            // Determine example answers based on question ID
     let examples = [];
          if (question.id === 'group_size') {
       examples = ['3', '5 people', '10'];
            } else if (question.id === 'all_ages') {
              examples = ['8, 10, 35, 40', '4, 6, 35', '18, 25, 30'];
            }

            examples.forEach(example => {
         const btn = document.createElement('button');
          btn.type = 'button';
    btn.className = 'btn btn-outline-secondary btn-sm quick-action-btn';
    btn.textContent = example;
       btn.onclick = () => {
      document.getElementById('userInput').value = example;
                };
 quickActionsDiv.appendChild(btn);
  });

document.getElementById('userInput').focus();
        }

        // Update session display
        function updateSessionDisplay(sessionId, status) {
            const sessionIdDisplay = document.getElementById('sessionIdDisplay');
            const statusIndicator = document.getElementById('statusIndicator');
  
         sessionIdDisplay.textContent = sessionId ? sessionId : 'Not Started';
    
        if (status === 'active') {
       statusIndicator.className = 'status-indicator active';
  } else if (status === 'complete') {
 statusIndicator.className = 'status-indicator complete';
    } else {
   statusIndicator.className = 'status-indicator';
}
        }

        // Show loading state
  function showLoading(message) {
    const conversationArea = document.getElementById('conversationArea');
         conversationArea.innerHTML = `
         <div class="text-center text-muted py-5">
  <div class="spinner-border text-primary mb-3" role="status">
     <span class="visually-hidden">Loading...</span>
   </div>
          <p>${message}</p>
       </div>
   `;
        }

        // Clear conversation
        function clearConversation() {
      const conversationArea = document.getElementById('conversationArea');
       conversationArea.innerHTML = '';
            document.getElementById('inputArea').style.display = 'none';
        }

        // Reset conversation
        function resetConversation() {
  currentSessionId = null;
          currentNextUrl = null;
            clearConversation();
  updateSessionDisplay(null, '');
   
        const conversationArea = document.getElementById('conversationArea');
            conversationArea.innerHTML = `
            <div class="text-center text-muted py-5">
       <i class="bi bi-chat-dots display-1"></i>
   <p class="mt-3">Click "Start New Conversation" to begin</p>
       </div>
`;
        }

        // Run predefined scenario
        async function runScenario(scenarioName) {
            try {
     showLoading(`Running ${scenarioName} scenario...`);

                addDebugEntry('success', `?? Running Scenario: ${scenarioName}`, {
     scenario: scenarioName,
       url: `${BASE_URL}/demo/scenario/${scenarioName}`
  });

  const response = await fetch(`${BASE_URL}/demo/scenario/${scenarioName}`);
    const data = await response.json();

                addDebugEntry('success', `? Scenario Response (${response.status})`, {
     status: response.status,
       body: data
       });

             clearConversation();
         updateSessionDisplay(data.sessionId, 'complete');

     // Display conversation history
        data.conversation.forEach(step => {
        if (step.question) {
        addMessage('assistant', step.question);
      }
         if (step.answer) {
     addMessage('user', step.answer);
      }
      });

                // Display final recommendation
           if (data.finalRecommendation) {
          const card = {
                  title: data.finalRecommendation.title,
        subtitle: '',
   careTypeMessage: data.finalRecommendation.description,
            bodyText: data.finalRecommendation.details
   };
      addRecommendation(card);
      }

                addMessage('assistant', `? Scenario '${scenarioName}' completed successfully`);
       } catch (error) {
   addDebugEntry('error', '? Scenario Error', {
            scenario: scenarioName,
         error: error.message,
   stack: error.stack
       });
       console.error('Error running scenario:', error);
       addMessage('error', 'Failed to run scenario: ' + error.message);
            }
      }

// END OF JAVASCRIPT TO INSERT
